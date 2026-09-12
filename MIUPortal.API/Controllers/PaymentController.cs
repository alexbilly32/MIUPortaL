using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Services;
using MIUPortal.API.Utilities;
using System.Security.Claims;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly PaymentFinalizationService _finalizationService;
        private readonly FeeScopingService _feeScopingService;
        private const decimal FUNCTIONAL_FEES_PER_YEAR = 205000m;


        private int? GetCurrentBursarId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        public PaymentController(MIUContext context, PaymentFinalizationService finalizationService, FeeScopingService feeScopingService)
        {
            _context = context;
            _finalizationService = finalizationService;
            _feeScopingService = feeScopingService;
        }

        [HttpPost("record")]
        [Authorize(Roles = "Bursar")]
        public async Task<IActionResult> RecordTuitionPayment([FromBody] RecordPaymentRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.StudentRegNumber))
                    return BadRequest(new { message = "StudentRegNumber is required" });

                if (request.Amount <= 0)
                    return BadRequest(new { message = "Amount must be greater than 0" });

                if (string.IsNullOrWhiteSpace(request.PaymentCategory))
                    return BadRequest(new { message = "PaymentCategory (TUITION or FUNCTIONAL) is required" });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == request.StudentRegNumber);
                if (student == null) return NotFound(new { message = "Student not found" });

                var programme = await _context.Programmes.FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);
                if (programme == null) return NotFound(new { message = "Programme not found" });

                // ===== AUTHORITATIVE SemesterId =====
               
                var currentEnrollment = await _context.StudentSemesterEnrollments
                    .FirstOrDefaultAsync(e => e.RegNumber == request.StudentRegNumber && e.IsCurrent);

                if (currentEnrollment == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "No active semester enrollment found for this student. " +
                                   "This usually means the one-time enrollment backfill hasn't been run for them yet — run it before recording payments."
                    });
                }

                int authoritativeSemesterId = currentEnrollment.SemesterId;

                if (request.SemesterId != authoritativeSemesterId)
                {
                    Console.WriteLine(
                        $"[WARN] RecordTuitionPayment: request.SemesterId={request.SemesterId} did not match " +
                        $"{request.StudentRegNumber}'s current enrollment SemesterId={authoritativeSemesterId}. " +
                        $"Using the authoritative value instead.");
                }

                var semester = await _context.AcademicSemesters.FirstOrDefaultAsync(s => s.SemesterId == authoritativeSemesterId);
                if (semester == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Student's current enrollment points to SemesterId {authoritativeSemesterId}, which does not exist in AcademicSemesters. This indicates corrupted enrollment data — contact the registrar. Payment was NOT recorded."
                    });
                }

                decimal semesterTuition = FeeCategoryHelper.GetRequiredTuition(student, programme.TuitionFeePerSemester);
                decimal functionalFeesPerYear = FUNCTIONAL_FEES_PER_YEAR;

                
                var scoped = await _feeScopingService.GetScopedPaymentsAsync(student);
                decimal tuitionPaidSoFar = scoped.TuitionPaidThisSemester;
                decimal functionalPaidSoFar = scoped.FunctionalPaidThisYear;

                decimal tuitionRemaining = Math.Max(0, semesterTuition - tuitionPaidSoFar);
                decimal functionalRemaining = Math.Max(0, functionalFeesPerYear - functionalPaidSoFar);

                decimal allowedAmount;
                string validationMessage;

                if (request.PaymentCategory == "TUITION")
                {
                    if (tuitionRemaining <= 0)
                        return BadRequest(new { success = false, message = "All tuition fees for this semester have been paid.", remaining = 0 });
                    if (request.Amount > tuitionRemaining)
                        return BadRequest(new { success = false, message = $"Tuition payment exceeds remaining balance. Max: UGX {tuitionRemaining}", remaining = tuitionRemaining });

                    allowedAmount = request.Amount;
                    validationMessage = "Tuition payment validated";
                }
                else if (request.PaymentCategory == "FUNCTIONAL")
                {
                    if (functionalRemaining <= 0)
                        return BadRequest(new { success = false, message = "All functional fees for this year have been paid.", remaining = 0 });
                    if (request.Amount > functionalRemaining)
                        return BadRequest(new { success = false, message = $"Functional fee payment exceeds remaining balance. Max: UGX {functionalRemaining}", remaining = functionalRemaining });

                    allowedAmount = request.Amount;
                    validationMessage = "Functional fee payment validated";
                }
                else
                {
                    return BadRequest(new { message = "PaymentCategory must be 'TUITION' or 'FUNCTIONAL'" });
                }

                var payment = new Payment
                {
                    RegNumber = request.StudentRegNumber,
                    Amount = allowedAmount,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = request.PaymentMethod ?? "Bank Transfer",
                    Network = request.Network,
                    PhoneNumber = request.PhoneNumber,
                    TransactionReference = request.TransactionReference ?? GenerateTransactionReference(),
                    Status = "Approved", 
                    Description = request.Description ?? $"{request.PaymentCategory} Fee Payment",
                    PaymentType = "TUITION_FEE",
                    PaymentCategory = request.PaymentCategory,
                    SemesterId = authoritativeSemesterId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                payment.ProcessedByBursarId = GetCurrentBursarId();
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync(); 

                var result = await _finalizationService.FinalizeApprovedPaymentAsync(payment.PaymentId);

                return Ok(new
                {
                    success = true,
                    message = $"Payment recorded successfully. {validationMessage}",
                    paymentId = result.PaymentId,
                    transactionReference = result.TransactionReference,
                    amount = result.Amount,
                    paymentCategory = result.PaymentCategory,
                    studentRegNumber = result.StudentRegNumber,
                    studentName = result.StudentName,
                    receiptPath = result.ReceiptPath,
                    receiptGenerated = result.ReceiptGenerated,
                    receiptEmailSent = result.ReceiptEmailSent,
                    tuitionRequired = result.TuitionRequired,
                    tuitionPaid = result.TuitionPaid,
                    tuitionRemaining = result.TuitionRemaining,
                    functionalRequired = result.FunctionalRequired,
                    functionalPaid = result.FunctionalPaid,
                    functionalRemaining = result.FunctionalRemaining,
                    totalRequired = result.TotalRequired,
                    totalPaid = result.TotalPaid,
                    totalRemaining = result.TotalRemaining,
                    canEnroll = result.CanEnroll,
                    enrollmentStatus = result.EnrollmentStatus,
                    status = result.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error recording payment: {ex.Message}" });
            }
        }

        
        [HttpPost("submit-proof")]
        public async Task<IActionResult> SubmitPaymentProof([FromBody] SubmitPaymentProofRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.StudentRegNumber))
                    return BadRequest(new { message = "StudentRegNumber is required" });

                if (request.Amount <= 0)
                    return BadRequest(new { message = "Amount must be greater than 0" });

                if (string.IsNullOrWhiteSpace(request.PaymentCategory))
                    return BadRequest(new { message = "PaymentCategory (TUITION or FUNCTIONAL) is required" });

                if (string.IsNullOrWhiteSpace(request.PaymentProofPath))
                    return BadRequest(new { message = "PaymentProofPath (uploaded proof file) is required" });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == request.StudentRegNumber);
                if (student == null) return NotFound(new { message = "Student not found" });

                var programme = await _context.Programmes.FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);
                if (programme == null) return NotFound(new { message = "Programme not found" });

                
                var currentEnrollment = await _context.StudentSemesterEnrollments
                    .FirstOrDefaultAsync(e => e.RegNumber == request.StudentRegNumber && e.IsCurrent);

                if (currentEnrollment == null)
                {
                    return BadRequest(new
                    {
                        message = "No active semester enrollment found for this student. " +
                                   "This usually means the one-time enrollment backfill hasn't been run for them yet — run it before submitting proof of payment."
                    });
                }

                int authoritativeSemesterId = currentEnrollment.SemesterId;

                if (request.SemesterId != authoritativeSemesterId)
                {
                    Console.WriteLine(
                        $"[WARN] SubmitPaymentProof: request.SemesterId={request.SemesterId} did not match " +
                        $"{request.StudentRegNumber}'s current enrollment SemesterId={authoritativeSemesterId}. " +
                        $"Using the authoritative value instead.");
                }

                var semester = await _context.AcademicSemesters.FirstOrDefaultAsync(s => s.SemesterId == authoritativeSemesterId);
                if (semester == null)
                    return BadRequest(new { message = $"Student's current enrollment points to SemesterId {authoritativeSemesterId}, which does not exist in AcademicSemesters. This indicates corrupted enrollment data — contact the registrar." });

                decimal semesterTuition = FeeCategoryHelper.GetRequiredTuition(student, programme.TuitionFeePerSemester);
                decimal functionalFeesPerYear = FUNCTIONAL_FEES_PER_YEAR;

               
                var scoped = await _feeScopingService.GetScopedPaymentsAsync(student);
                decimal tuitionPaidSoFar = scoped.TuitionPaidThisSemester;
                decimal functionalPaidSoFar = scoped.FunctionalPaidThisYear;

                decimal tuitionRemaining = Math.Max(0, semesterTuition - tuitionPaidSoFar);
                decimal functionalRemaining = Math.Max(0, functionalFeesPerYear - functionalPaidSoFar);

                if (request.PaymentCategory == "TUITION" && request.Amount > tuitionRemaining)
                    return BadRequest(new { message = $"Amount exceeds remaining tuition balance. Max: UGX {tuitionRemaining}" });

                if (request.PaymentCategory == "FUNCTIONAL" && request.Amount > functionalRemaining)
                    return BadRequest(new { message = $"Amount exceeds remaining functional fee balance. Max: UGX {functionalRemaining}" });

                string proofPath = request.PaymentProofPath.StartsWith("/")
                    ? request.PaymentProofPath
                    : $"/uploads/payment-proofs/{request.PaymentProofPath}";

                var payment = new Payment
                {
                    RegNumber = request.StudentRegNumber,
                    Amount = request.Amount,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = request.PaymentMethod ?? "Bank Transfer",
                    Network = request.Network,
                    PhoneNumber = request.PhoneNumber,
                    TransactionReference = request.TransactionReference ?? GenerateTransactionReference(),
                    Status = "Pending",
                    Description = request.Description ?? $"{request.PaymentCategory} Fee Payment (pending verification)",
                    PaymentType = "TUITION_FEE",
                    PaymentCategory = request.PaymentCategory,
                    SemesterId = authoritativeSemesterId,
                    PaymentProofPath = proofPath,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Payment proof submitted. Awaiting bursar verification.",
                    paymentId = payment.PaymentId,
                    transactionReference = payment.TransactionReference,
                    status = payment.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error submitting payment proof: {ex.Message}" });
            }
        }

        
        [HttpPost("upload-proof")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10_000_000)] // 10MB cap
        public async Task<IActionResult> UploadPaymentProof(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file was uploaded." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { message = "Only JPG, PNG, or PDF files are accepted." });

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "payment-proofs");
            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            var fileName = $"proof_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new
            {
                success = true,
                paymentProofPath = $"/uploads/payment-proofs/{fileName}"
            });
        }

        [HttpGet("history/{studentRegNumber}")]
        public async Task<IActionResult> GetPaymentHistory(string studentRegNumber)
        {
            try
            {
                studentRegNumber = System.Net.WebUtility.UrlDecode(studentRegNumber);
                var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == studentRegNumber);
                if (student == null) return NotFound(new { message = "Student not found" });

                var payments = await _context.Payments
                    .Where(p => p.RegNumber == studentRegNumber)
                    .OrderByDescending(p => p.PaymentDate)
                    .Select(p => new { p.PaymentId, p.Amount, p.PaymentDate, p.PaymentMethod, p.Network, p.TransactionReference, p.PaymentCategory, p.Status, p.PaymentType, p.ReceiptPath, p.ReceiptGenerated, p.PaymentProofPath, p.RejectionReason })
                    .ToListAsync();

                return Ok(new
                {
                    studentRegNumber = student.RegNumber,
                    studentName = $"{student.FirstName} {student.LastName}",
                    totalPaid = student.TotalPaid,
                    feesBalance = student.FeesBalance,
                    paymentHistory = payments
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving payment history: {ex.Message}" });
            }
        }

        [HttpGet("verify/{transactionReference}")]
        public async Task<IActionResult> VerifyPayment(string transactionReference)
        {
            try
            {
                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.TransactionReference == transactionReference);
                if (payment == null) return NotFound(new { message = "Payment not found" });

                return Ok(new
                {
                    transactionReference = payment.TransactionReference,
                    amount = payment.Amount,
                    paymentCategory = payment.PaymentCategory,
                    status = payment.Status,
                    paymentDate = payment.PaymentDate,
                    studentRegNumber = payment.RegNumber,
                    paymentMethod = payment.PaymentMethod,
                    receiptPath = payment.ReceiptPath,
                    receiptGenerated = payment.ReceiptGenerated
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error verifying payment: {ex.Message}" });
            }
        }

        [HttpGet("receipt/{transactionReference}")]
        public async Task<IActionResult> DownloadReceipt(string transactionReference)
        {
            try
            {
                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.TransactionReference == transactionReference);
                if (payment == null || string.IsNullOrEmpty(payment.ReceiptPath))
                    return NotFound(new { message = "Receipt not found" });

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", payment.ReceiptPath.TrimStart('/'));
                if (!System.IO.File.Exists(filePath))
                    return NotFound(new { message = "Receipt file not found" });

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, "application/pdf", $"Receipt_{transactionReference}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error downloading receipt: {ex.Message}" });
            }
        }

        private string GenerateTransactionReference()
        {
            var year = DateTime.Now.Year;
            var random = new Random();
            var count = _context.Payments.Count(p => p.TransactionReference != null);
            return $"TXN-{year}-{count + 1:D6}-{random.Next(1000, 9999)}";
        }
    }

    public class RecordPaymentRequest
    {
        public string? StudentRegNumber { get; set; }
        public decimal Amount { get; set; }
        public string PaymentCategory { get; set; } = "TUITION";
        public string? PaymentMethod { get; set; }
        public string? Network { get; set; }
        public string? PhoneNumber { get; set; }
        public string? TransactionReference { get; set; }
        public string? Description { get; set; }
        public int SemesterId { get; set; }
    }

    public class SubmitPaymentProofRequest
    {
        public string? StudentRegNumber { get; set; }
        public decimal Amount { get; set; }
        public string PaymentCategory { get; set; } = "TUITION";
        public string? PaymentMethod { get; set; }
        public string? Network { get; set; }
        public string? PhoneNumber { get; set; }
        public string? TransactionReference { get; set; }
        public string? Description { get; set; }
        public int SemesterId { get; set; }
        public string? PaymentProofPath { get; set; }
    }
}