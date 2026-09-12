

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Services;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly ILogger<DocumentController> _logger;
        private readonly IDocumentService _documentService;
        public DocumentController(
    MIUContext context,
    ILogger<DocumentController> logger,
    IDocumentService documentService)
        {
            _context = context;
            _logger = logger;
            _documentService = documentService;
        }

        
        [HttpGet("available/{regNumber}")]
        public async Task<IActionResult> GetAvailableDocuments(string regNumber)
        {
            try
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.RegNumber == regNumber);

                if (student == null)
                {
                    return NotFound(new { message = "Student not found" });
                }

                var documents = await _context.Documents
                    .Where(d => d.RegNumber == regNumber)
                    .ToListAsync();

                var documentTypes = await _context.DocumentTypes.ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        student = new { student.RegNumber, name = student.FirstName },
                        documents,
                        availableTypes = documentTypes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAvailableDocuments");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        
        [HttpPost("request")]
        public async Task<IActionResult> RequestDocument([FromBody] DocumentRequestRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.RegNumber))
                {
                    return BadRequest(new { message = "Invalid document request" });
                }

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.RegNumber == request.RegNumber);

                if (student == null)
                {
                    return NotFound(new { message = "Student not found" });
                }

                var documentType = await _context.DocumentTypes
                    .FirstOrDefaultAsync(d => d.DocumentTypeId == request.DocumentTypeId);

                if (documentType == null)
                {
                    return NotFound(new { message = "Document type not found" });
                }

                
                if (!request.FeePaid)
                {
                    return BadRequest(new
                    {
                        message = "Document fee payment required",
                        fee = documentType.DocumentFee,
                        minPayment = documentType.DocumentFee / 2,
                        instruction = "Pay at least 50% of the fee before generating document"
                    });
                }

               
                int resolvedTypeId = await GetDocumentTypeIdAsync(request.DocumentType ?? documentType.DocumentTypeName);

                var document = new Document
                {
                    RegNumber = request.RegNumber,
                    DocumentTypeId = resolvedTypeId != 0 ? resolvedTypeId : documentType.DocumentTypeId,
                    DocumentType = documentType.DocumentTypeName,
                    DocumentPath = request.DocumentPath,
                    Status = "Pending",
                    FeePaid = true,
                    CreatedAt = DateTime.Now
                };

                _context.Documents.Add(document);

                await _context.SaveChangesAsync();

                
                await _documentService.GenerateDocumentAsync(
                    document.DocumentId);

                Console.WriteLine($"✅ Document request saved: {document.DocumentId}");

                return Ok(new
                {
                    success = true,
                    message = "Document request submitted. It will be ready soon.",
                    data = document
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RequestDocument");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        
        [HttpPost("save-generated")]
        public async Task<IActionResult> SaveGeneratedDocument([FromBody] SaveDocumentRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.RegNumber))
                {
                    return BadRequest(new { success = false, message = "Registration number required" });
                }

                if (string.IsNullOrWhiteSpace(request.DocumentType))
                {
                    return BadRequest(new { success = false, message = "Document type is required" });
                }

                
                int documentTypeId = await GetDocumentTypeIdAsync(request.DocumentType);

                if (documentTypeId == 0)
                {
                    _logger.LogWarning(
                        $"SaveGeneratedDocument: no DocumentTypes row matches '{request.DocumentType}'. " +
                        "Check spelling/casing against the DocumentTypes table.");

                    return BadRequest(new
                    {
                        success = false,
                        message = $"Unknown document type '{request.DocumentType}'. It does not match any row in DocumentTypes."
                    });
                }

                // ✅ CREATE DOCUMENT RECORD
                var document = new Document
                {
                    RegNumber = request.RegNumber,
                    DocumentType = request.DocumentType,  
                    DocumentPath = request.DocumentPath,  
                    Status = "GENERATED",
                    DateGenerated = DateTime.Now,
                    DocumentTypeId = documentTypeId,
                    FeePaid = true,
                    QrVerificationCode = Guid.NewGuid().ToString(),
                    ExpiryDate = DateTime.Now.AddMonths(6),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                // ✅ SAVE TO DATABASE
                _context.Documents.Add(document);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Generated document saved: DocumentId={document.DocumentId}, Type={request.DocumentType}, RegNumber={request.RegNumber}");

                return Ok(new
                {
                    success = true,
                    message = "Document saved successfully",
                    data = new
                    {
                        documentId = document.DocumentId,
                        documentType = document.DocumentType,
                        status = document.Status,
                        dateGenerated = document.DateGenerated
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveGeneratedDocument");
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

       
        [HttpGet("{regNumber}")]
        public async Task<IActionResult> GetStudentDocuments(string regNumber)
        {
            try
            {
                Console.WriteLine($"📄 Fetching documents for: {regNumber}");

                var documents = await _context.Documents
                    .Where(d => d.RegNumber == regNumber)
                    .OrderByDescending(d => d.DateGenerated)
                    .ToListAsync();

                Console.WriteLine($"✅ Found {documents.Count} documents");

                return Ok(new
                {
                    success = true,
                    data = documents,
                    count = documents.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStudentDocuments");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        
        [HttpGet("download/{documentId}")]
        public async Task<IActionResult> DownloadDocument(int documentId)
        {
            try
            {
                var document = await _context.Documents
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                {
                    return NotFound(new { message = "Document not found" });
                }

                if (document.Status != "Ready" && document.Status != "GENERATED")
                {
                    return BadRequest(new { message = "Document is not ready for download yet" });
                }

                document.DateDownloaded = DateTime.Now;
                _context.Documents.Update(document);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Document downloaded successfully",
                    data = document
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DownloadDocument");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        
        private async Task<int> GetDocumentTypeIdAsync(string documentType)
        {
            if (string.IsNullOrWhiteSpace(documentType))
                return 0;

            var normalized = documentType.Trim().ToUpper();

            var match = await _context.DocumentTypes
                .FirstOrDefaultAsync(d => d.DocumentTypeName.Trim().ToUpper() == normalized);

            return match?.DocumentTypeId ?? 0;
        }
    }

    // ============================================
    // REQUEST MODELS
    // ============================================

    public class DocumentRequestRequest
    {
        public string? RegNumber { get; set; }
        public int DocumentTypeId { get; set; }
        public bool FeePaid { get; set; }
        public string? DocumentType { get; set; } 
        public string? DocumentPath { get; set; } 
    }

   
    public class SaveDocumentRequest
    {
        public string? RegNumber { get; set; }
        public string? DocumentType { get; set; }  
        public string? DocumentPath { get; set; }  
    }
}