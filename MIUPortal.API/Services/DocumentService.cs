using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Services
{
    public interface IDocumentService
    {
        Task<List<Document>> GetStudentDocumentsAsync(string regNumber);
        Task<bool> RequestDocumentAsync(string regNumber, int documentTypeId);
        Task<bool> GenerateDocumentAsync(int documentId);
        Task<Document?> GetDocumentAsync(int documentId);
    }

    public class DocumentService : IDocumentService
    {
        private readonly MIUContext _context;
        private readonly ILogger<DocumentService> _logger;
        private readonly QrCodeService _qrCodeService;
        private readonly IWebHostEnvironment _environment;

       
           public DocumentService(
    MIUContext context,
    ILogger<DocumentService> logger,
    QrCodeService qrCodeService,
    IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _qrCodeService = qrCodeService;
            _environment = environment;
        }
      

        public async Task<List<Document>> GetStudentDocumentsAsync(string regNumber)
        {
            try
            {
                return await _context.Documents
                    .Where(d => d.RegNumber == regNumber)
                    .OrderByDescending(d => d.DateGenerated)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting student documents");
                return new List<Document>();
            }
        }

        public async Task<bool> RequestDocumentAsync(string regNumber, int documentTypeId)
        {
            try
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.RegNumber == regNumber);

                if (student == null)
                    return false;

                var documentType = await _context.DocumentTypes
                    .FirstOrDefaultAsync(d => d.DocumentTypeId == documentTypeId);

                if (documentType == null)
                    return false;

                var document = new Document
                {
                    RegNumber = regNumber,
                    DocumentTypeId = documentTypeId,
                    DocumentType = documentType.DocumentTypeName,
                    Status = "Pending",
                    DocumentFee = documentType.DocumentFee,
                    QrVerificationCode = Guid.NewGuid().ToString(),
                    ExpiryDate = DateTime.Now.AddMonths(6),
                    CreatedAt = DateTime.Now
                };

                _context.Documents.Add(document);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting document");
                return false;
            }
        }

        public async Task<bool> GenerateDocumentAsync(int documentId)
        {
            try
            {
                var document = await _context.Documents
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                    return false;
                document.Status = "Ready";
                document.DateGenerated = DateTime.Now;
                document.UpdatedAt = DateTime.Now;


                // Generate official document number
                document.DocumentNumber =
                    _qrCodeService.GenerateDocumentNumber(
                        document.DocumentType ?? "DOCUMENT");


                // Generate verification ID
                document.VerificationId =
                    _qrCodeService.GenerateVerificationId();


                // Generate verification hash
                document.VerificationHash =
                    _qrCodeService.GenerateVerificationHash(
                        document.VerificationId);


                // Build verification URL
                var verificationUrl =
                    _qrCodeService.BuildVerificationUrl(
                        document.VerificationId);


                // Generate QR image
                var qrBytes =
                    _qrCodeService.GenerateQrImage(
                        verificationUrl);


                // Create QR directory
                var qrFolder =
                    Path.Combine(
                        _environment.WebRootPath,
                        "uploads",
                        "qrcodes");


                if (!Directory.Exists(qrFolder))
                {
                    Directory.CreateDirectory(qrFolder);
                }


                // Save QR image
                var qrFileName =
                    $"{document.DocumentNumber}.png";


                var qrPath =
                    Path.Combine(
                        qrFolder,
                        qrFileName);


                await File.WriteAllBytesAsync(
                    qrPath,
                    qrBytes);


                // Save relative path
                document.QrCodePath =
                    $"uploads/qrcodes/{qrFileName}";

                _context.Documents.Update(document);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating document");
                return false;
            }
        }

        public async Task<Document?> GetDocumentAsync(int documentId)
        {
            try
            {
                return await _context.Documents
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document");
                return null;
            }
        }
    }
}
