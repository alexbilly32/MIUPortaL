using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Services;


namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApplicationController(
        MIUContext context,
        IEmailService emailService,
        ILogger<ApplicationController> logger,
        IWebHostEnvironment env,
        PassportPhotoService passportPhotoService)
        : ControllerBase
    {
        private readonly MIUContext _context = context;
        private readonly IEmailService _emailService = emailService;
        private readonly ILogger<ApplicationController> _logger = logger;
        private readonly IWebHostEnvironment _env = env;
        private readonly PassportPhotoService _passportPhotoService = passportPhotoService;

       
        private static readonly string[] AllowedExtensions = [ ".jpg", ".jpeg", ".png", ".pdf" ];

        [HttpPost("upload-passport-photo")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPassportPhoto(
     [FromForm] int applicationId,
     [FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "No passport photo uploaded."
                    });
                }


                var application = await _context.Applications
                    .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);


                if (application == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Application not found"
                    });
                }


                var extension = Path.GetExtension(file.FileName)
                    .ToLowerInvariant();


                if (extension != ".jpg" &&
                    extension != ".jpeg" &&
                    extension != ".png")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Only JPG and PNG images are allowed."
                    });
                }



                var webRootPath = _env.WebRootPath;

                if (string.IsNullOrEmpty(webRootPath))
                {
                    webRootPath =
                        Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot");
                }



                var uploadsFolder = Path.Combine(
                     webRootPath,
                     "uploads",
                     "passport-photos"
                 );

                
                string fileName = await _passportPhotoService.ResizePassportPhoto(file, uploadsFolder);

                var relativePath =
                    "/uploads/passport-photos/" + fileName;



               
                application.PassportPhotoPath = relativePath;
                application.UpdatedAt = DateTime.UtcNow;



                await _context.SaveChangesAsync();

                var student = await _context.Students
    .FirstOrDefaultAsync(
        s => s.Email == application.Email
    );


                if (student != null)
                {
                    student.PassportPhotoPath = relativePath;
                    await _context.SaveChangesAsync();
                }



                return Ok(new
                {
                    success = true,
                    message = "Passport photo uploaded successfully.",
                    passportPhotoPath = relativePath
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Passport photo upload failed");

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // =====================================================
        // UPLOAD PAYMENT PROOF
        // =====================================================
        [HttpPost("upload-payment-proof")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPaymentProof([FromForm] int applicationId, [FromForm] IFormFile file)
        {
            try
            {
                
                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "No file uploaded" });

             
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                {
                    return BadRequest(new { success = false, message = "Invalid file type. Only JPG, PNG, and PDF are allowed." });
                }

                
                var application = await _context.Applications
                    .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

                if (application == null)
                    return NotFound(new { success = false, message = "Application not found" });

                
                string webRootPath = _env.WebRootPath;
                if (string.IsNullOrEmpty(webRootPath))
                {
                   
                    webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                var uploadsFolder = Path.Combine(webRootPath, "uploads", "payment-proofs");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

               
                _logger.LogInformation("Saving payment proof to target path: {FilePath}", filePath);

              
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = "/uploads/payment-proofs/" + fileName;

                
                application.PaymentProofPath = relativePath;
                application.PaymentStatus = "PENDING_VERIFICATION";
                application.UpdatedAt = DateTime.UtcNow; 

              
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Payment proof uploaded successfully",
                    fileName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading payment proof for application ID: {ApplicationId}", applicationId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Upload failed",
                    error = ex.Message 
                });
            }
        }

        // =====================================================
        // UPLOAD GOVERNMENT LOAN SCHEME PROOF
        // =====================================================
        [HttpPost("upload-loan-proof")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadLoanProof([FromForm] int applicationId, [FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "No file uploaded" });

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                {
                    return BadRequest(new { success = false, message = "Invalid file type. Only JPG, PNG, and PDF are allowed." });
                }

                var application = await _context.Applications
                    .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

                if (application == null)
                    return NotFound(new { success = false, message = "Application not found" });

                if (application.ApplicationCategory != "GovernmentLoan")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Loan proof can only be uploaded for applications on the Government Loan Scheme category."
                    });
                }

                string webRootPath = _env.WebRootPath;
                if (string.IsNullOrEmpty(webRootPath))
                {
                    webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                var uploadsFolder = Path.Combine(webRootPath, "uploads", "loan-proofs");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = "/uploads/loan-proofs/" + fileName;

                application.LoanProofPath = relativePath;
                application.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Loan scheme proof uploaded successfully.",
                    loanProofPath = relativePath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading loan proof for application ID: {ApplicationId}", applicationId);
                return StatusCode(500, new { success = false, message = "Upload failed", error = ex.Message });
            }
        }
    }
}