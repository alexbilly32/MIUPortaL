using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Services;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentVerificationController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly QrCodeService _qrCodeService;
        private readonly ILogger<DocumentVerificationController> _logger;

        public DocumentVerificationController(
            MIUContext context,
            QrCodeService qrCodeService,
            ILogger<DocumentVerificationController> logger)
        {
            _context = context;
            _qrCodeService = qrCodeService;
            _logger = logger;
        }

        
        [HttpGet("verify/{verificationId}")]
        public async Task<IActionResult> VerifyDocument(string verificationId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(verificationId))
                {
                    return BadRequest(new
                    {
                        valid = false,
                        message = "Invalid verification ID."
                    });
                }


                var document = await _context.Documents
                    .FirstOrDefaultAsync(
                        d => d.VerificationId == verificationId);


                if (document == null)
                {
                    return NotFound(new
                    {
                        valid = false,
                        message = "Document not found."
                    });
                }


              
                if (document.IsRevoked)
                {
                    return Ok(new
                    {
                        valid = false,
                        message = "This document has been revoked.",
                        revokedAt = document.RevokedAt
                    });
                }


                
                bool validSignature =
                    _qrCodeService.ValidateVerification(
                        verificationId,
                        document.VerificationHash ?? "");


                if (!validSignature)
                {
                    _logger.LogWarning(
                        "Invalid document verification attempt for {VerificationId}",
                        verificationId);


                    return Ok(new
                    {
                        valid = false,
                        message = "Invalid document signature."
                    });
                }


               
                document.VerificationCount++;

                document.LastVerifiedAt =
                    DateTime.Now;


                await _context.SaveChangesAsync();



                var student = await _context.Students
                    .FirstOrDefaultAsync(
                        s => s.RegNumber == document.RegNumber);



                return Ok(new
                {
                    valid = true,
                    message = "Document verified successfully.",

                    documentNumber = document.DocumentNumber,

                    documentType = document.DocumentType,

                    studentName = student != null
                        ? $"{student.FirstName} {student.LastName}"
                        : "N/A",

                    regNumber = document.RegNumber,

                    dateGenerated = document.DateGenerated,

                    verificationCount =
                        document.VerificationCount,

                    status = document.Status
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error verifying document");


                return StatusCode(500,
                    new
                    {
                        valid = false,
                        message = "Verification failed."
                    });
            }
        }
    }
}