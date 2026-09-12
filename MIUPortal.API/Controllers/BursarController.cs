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
    [Route("api/bursar")]
    [Authorize(Roles = "Bursar")] 
    public class BursarController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly PaymentFinalizationService _finalizationService;
        private readonly AuditLogService _auditLog;
        private readonly IEmailService _emailService;
        private int? GetCurrentBursarId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }


       
        public BursarController(MIUContext context, PaymentFinalizationService finalizationService, AuditLogService auditLog, IEmailService emailService)
        {
            _context = context;
            _finalizationService = finalizationService;
            _auditLog = auditLog;
            _emailService = emailService;
        }

      
        [HttpGet("dashboard/summary")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            var today = DateTime.Today;

            decimal totalTuitionCollected = await _context.Payments
                .Where(p => p.PaymentCategory == "TUITION" && p.Status == "Approved")
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            decimal totalFunctionalCollected = await _context.Payments
                .Where(p => p.PaymentCategory == "FUNCTIONAL" && p.Status == "Approved")
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            decimal todaysCollections = await _context.Payments
                .Where(p => p.Status == "Approved" && p.PaymentDate.Date == today)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            int studentsFullyCleared = await _context.Students
                .Where(s => (s.FeesBalance ?? 0) <= 0)
                .CountAsync();

            int studentsWithOutstandingFees = await _context.Students
                .Where(s => (s.FeesBalance ?? 0) > 0)
                .CountAsync();

            decimal outstandingBalances = await _context.Students
                .Where(s => (s.FeesBalance ?? 0) > 0)
                .SumAsync(s => (decimal?)s.FeesBalance) ?? 0;

            int pendingVerifications = await _context.Payments.CountAsync(p => p.Status == "Pending");

            return Ok(new
            {
                totalTuitionCollected,
                totalFunctionalCollected,
                totalCollected = totalTuitionCollected + totalFunctionalCollected,
                outstandingBalances,
                studentsFullyCleared,
                studentsWithOutstandingFees,
                todaysCollections,
                pendingVerifications
            });
        }

        
        [HttpGet("student/{regNumber}/ledger")]
        public async Task<IActionResult> GetStudentLedger(string regNumber)
        {
            regNumber = System.Net.WebUtility.UrlDecode(regNumber);

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.RegNumber == regNumber);

            if (student == null)
                return NotFound(new { message = "Student not found" });

            var ledgerEntries = await _context.FinancialLedger
                .Where(f => f.RegNumber == regNumber)
                .OrderBy(f => f.TransactionDate)
                .Select(f => new
                {
                    f.LedgerId,
                    f.TransactionType,
                    f.Amount,
                    f.PaymentMethod,
                    f.PaymentReference,
                    f.TransactionDate,
                    f.Status,
                    f.Notes,
                    f.PaymentId,
                    f.RelatedLedgerId
                })
                .ToListAsync();

           
            decimal runningBalance = 0;
            var statement = ledgerEntries.Select(e =>
            {
                if (e.TransactionType == "CHARGE" || e.TransactionType == "REVERSAL_PAYMENT")
                    runningBalance += e.Amount;
                else if (e.TransactionType == "PAYMENT" || e.TransactionType == "REVERSAL_CHARGE")
                    runningBalance -= e.Amount;

                return new
                {
                    e.LedgerId,
                    e.TransactionType,
                    e.Amount,
                    e.PaymentMethod,
                    e.PaymentReference,
                    e.TransactionDate,
                    e.Status,
                    e.Notes,
                    runningBalance
                };
            }).ToList();

            return Ok(new
            {
                studentRegNumber = student.RegNumber,
                studentName = $"{student.FirstName} {student.LastName}",
                programmeCode = student.ProgrammeCode,
                storedFeesBalance = student.FeesBalance,
                ledgerComputedBalance = runningBalance,
                ledgerEntryCount = statement.Count,
                statement
            });
        }

        
        [HttpGet("fee-structures")]
        public async Task<IActionResult> GetFeeStructures(
            [FromQuery] string? programmeCode,
            [FromQuery] string? academicYear,
            [FromQuery] bool includeInactive = false)
        {
            var query = _context.FeeStructures.AsQueryable();

            if (!includeInactive)
                query = query.Where(fs => fs.IsActive);

            if (!string.IsNullOrWhiteSpace(programmeCode))
                query = query.Where(fs => fs.ProgrammeCode == programmeCode);

            if (!string.IsNullOrWhiteSpace(academicYear))
                query = query.Where(fs => fs.AcademicYear == academicYear);

            var results = await query
                .OrderBy(fs => fs.ProgrammeCode)
                .ThenBy(fs => fs.AcademicYear)
                .ThenBy(fs => fs.Semester)
                .Select(fs => new
                {
                    fs.FeeStructureId,
                    fs.ProgrammeCode,
                    ProgrammeName = fs.Programme != null ? fs.Programme.ProgrammeName : null,
                    fs.AcademicYear,
                    fs.Semester,
                    fs.Tuition,
                    fs.FunctionalFees,
                    fs.OtherCharges,
                    Total = fs.Tuition + fs.FunctionalFees + fs.OtherCharges,
                    fs.IsActive,
                    fs.CreatedAt,
                    fs.UpdatedAt
                })
                .ToListAsync();

            return Ok(results);
        }

        // GET /api/bursar/fee-structures/{id}
        [HttpGet("fee-structures/{id}")]
        public async Task<IActionResult> GetFeeStructureById(int id)
        {
            var fs = await _context.FeeStructures
                .Include(f => f.Programme)
                .FirstOrDefaultAsync(f => f.FeeStructureId == id);

            if (fs == null)
                return NotFound(new { message = "Fee structure not found" });

            return Ok(new
            {
                fs.FeeStructureId,
                fs.ProgrammeCode,
                ProgrammeName = fs.Programme?.ProgrammeName,
                fs.AcademicYear,
                fs.Semester,
                fs.Tuition,
                fs.FunctionalFees,
                fs.OtherCharges,
                Total = fs.Tuition + fs.FunctionalFees + fs.OtherCharges,
                fs.IsActive,
                fs.CreatedAt,
                fs.UpdatedAt
            });
        }

        
        [HttpPost("fee-structures")]
        public async Task<IActionResult> CreateFeeStructure([FromBody] CreateFeeStructureRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ProgrammeCode))
                    return BadRequest(new { message = "ProgrammeCode is required" });

                if (string.IsNullOrWhiteSpace(request.AcademicYear))
                    return BadRequest(new { message = "AcademicYear is required" });

                if (request.Semester != 1 && request.Semester != 2)
                    return BadRequest(new { message = "Semester must be 1 or 2" });

                if (request.Tuition < 0 || request.FunctionalFees < 0 || request.OtherCharges < 0)
                    return BadRequest(new { message = "Fee amounts cannot be negative" });

                var programme = await _context.Programmes
                    .FirstOrDefaultAsync(p => p.ProgrammeCode == request.ProgrammeCode);
                if (programme == null)
                    return NotFound(new { message = $"Programme '{request.ProgrammeCode}' not found" });

                bool duplicateExists = await _context.FeeStructures.AnyAsync(fs =>
                    fs.ProgrammeCode == request.ProgrammeCode &&
                    fs.AcademicYear == request.AcademicYear &&
                    fs.Semester == request.Semester &&
                    fs.IsActive);

                if (duplicateExists)
                    return Conflict(new
                    {
                        message = $"An active fee structure already exists for {request.ProgrammeCode}, " +
                                   $"{request.AcademicYear}, Semester {request.Semester}. " +
                                   "Deactivate it first or update it instead of creating a duplicate."
                    });

                var feeStructure = new FeeStructure
                {
                    ProgrammeCode = request.ProgrammeCode,
                    AcademicYear = request.AcademicYear,
                    Semester = request.Semester,
                    Tuition = request.Tuition,
                    FunctionalFees = request.FunctionalFees,
                    OtherCharges = request.OtherCharges,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.FeeStructures.Add(feeStructure);
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync(
                    "FEE_STRUCTURE_CREATED",
                    $"Created fee structure for {request.ProgrammeCode}, {request.AcademicYear} Semester {request.Semester} (Tuition: UGX {request.Tuition}, Functional: UGX {request.FunctionalFees})",
                    bursarId: GetCurrentBursarId(),
                    entityType: "FeeStructure",
                    entityId: feeStructure.FeeStructureId);

                return Ok(new
                {
                    message = "Fee structure created successfully",
                    feeStructureId = feeStructure.FeeStructureId
                });
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Duplicate entry") == true)
            {
                return Conflict(new { message = "A fee structure for this programme, year, and semester already exists." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error creating fee structure: {ex.Message}" });
            }
        }

        
        [HttpPut("fee-structures/{id}")]
        public async Task<IActionResult> UpdateFeeStructure(int id, [FromBody] UpdateFeeStructureRequest request)
        {
            var fs = await _context.FeeStructures.FirstOrDefaultAsync(f => f.FeeStructureId == id);
            if (fs == null)
                return NotFound(new { message = "Fee structure not found" });

            if (request.Semester.HasValue)
            {
                if (request.Semester != 1 && request.Semester != 2)
                    return BadRequest(new { message = "Semester must be 1 or 2" });
                fs.Semester = request.Semester.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.AcademicYear))
                fs.AcademicYear = request.AcademicYear;

            if (request.IsActive.HasValue)
                fs.IsActive = request.IsActive.Value;

            fs.UpdatedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync(
                    "FEE_STRUCTURE_UPDATED",
                    $"Updated fee structure #{id} ({fs.ProgrammeCode}, {fs.AcademicYear} Semester {fs.Semester})",
                    bursarId: GetCurrentBursarId(),
                    entityType: "FeeStructure",
                    entityId: fs.FeeStructureId);

                return Ok(new { message = "Fee structure updated successfully" });
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Duplicate entry") == true)
            {
                return Conflict(new { message = "This change would duplicate an existing fee structure for the same programme, year, and semester." });
            }
        }

        
        [HttpDelete("fee-structures/{id}")]
        public async Task<IActionResult> DeactivateFeeStructure(int id)
        {
            var fs = await _context.FeeStructures.FirstOrDefaultAsync(f => f.FeeStructureId == id);
            if (fs == null)
                return NotFound(new { message = "Fee structure not found" });

            fs.IsActive = false;
            fs.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync(
                "FEE_STRUCTURE_DEACTIVATED",
                $"Deactivated fee structure #{id} ({fs.ProgrammeCode}, {fs.AcademicYear} Semester {fs.Semester})",
                bursarId: GetCurrentBursarId(),
                entityType: "FeeStructure",
                entityId: fs.FeeStructureId);

            return Ok(new { message = "Fee structure deactivated" });
        }

        private const decimal FUNCTIONAL_FEES_PER_YEAR = 205000m;

       
        [HttpGet("student/{regNumber}/clearance")]
        public async Task<IActionResult> GetClearanceStatus(
            string regNumber,
            [FromQuery] string academicYear,
            [FromQuery] int semester)
        {
            regNumber = System.Net.WebUtility.UrlDecode(regNumber);

            if (string.IsNullOrWhiteSpace(academicYear))
                return BadRequest(new { message = "academicYear query parameter is required" });

            if (semester != 1 && semester != 2)
                return BadRequest(new { message = "semester query parameter must be 1 or 2" });

            var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
            if (student == null)
                return NotFound(new { message = "Student not found" });

            // Prefer a real FeeStructure row; fall back to Programme.TuitionFeePerSemester
            // + the shared functional-fees constant for programmes not yet configured.
            var feeStructure = await _context.FeeStructures
                .FirstOrDefaultAsync(fs =>
                    fs.ProgrammeCode == student.ProgrammeCode &&
                    fs.AcademicYear == academicYear &&
                    fs.Semester == semester &&
                    fs.IsActive);

            decimal tuitionRequired;
            decimal functionalRequired;
            bool usedFallback = false;

            if (feeStructure != null)
            {
                tuitionRequired = FeeCategoryHelper.GetRequiredTuition(student, feeStructure.Tuition);
                functionalRequired = feeStructure.FunctionalFees;
            }
            else
            {
                var programme = await _context.Programmes.FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);
                if (programme == null) return NotFound(new { message = "Programme not found for this student" });

                tuitionRequired = FeeCategoryHelper.GetRequiredTuition(student, programme.TuitionFeePerSemester);
                functionalRequired = semester == 1 ? FUNCTIONAL_FEES_PER_YEAR * 0.5m : FUNCTIONAL_FEES_PER_YEAR;
                usedFallback = true;
            }

            decimal tuitionPaid = await _context.Payments
                .Where(p => p.RegNumber == regNumber && p.PaymentCategory == "TUITION" && p.Status == "Approved")
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            decimal functionalPaid = await _context.Payments
                .Where(p => p.RegNumber == regNumber && p.PaymentCategory == "FUNCTIONAL" && p.Status == "Approved")
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            
            bool registrationComputed = tuitionPaid >= (tuitionRequired * 0.5m)
                                     && functionalPaid >= (functionalRequired * 0.5m);

           
            bool examComputed = tuitionPaid >= tuitionRequired && functionalPaid >= functionalRequired;

           
            var overrides = await _context.ClearanceOverrides
                .Where(co => co.RegNumber == regNumber && co.IsActive &&
                             co.AcademicYear == academicYear && co.Semester == semester)
                .OrderByDescending(co => co.CreatedAt)
                .ToListAsync();

            var registrationOverride = overrides.FirstOrDefault(co => co.ClearanceType == "REGISTRATION");
            var examOverride = overrides.FirstOrDefault(co => co.ClearanceType == "EXAM");

            bool registrationCleared = registrationOverride?.IsGranted ?? registrationComputed;
            bool examCleared = examOverride?.IsGranted ?? examComputed;

            string OverallStatus(decimal paid, decimal required) =>
                paid <= 0 ? "Not Cleared" : paid >= required ? "Fully Cleared" : "Partially Cleared";

            return Ok(new
            {
                studentRegNumber = student.RegNumber,
                studentName = $"{student.FirstName} {student.LastName}",
                academicYear,
                semester,
                usedFallbackFeeStructure = usedFallback,
                tuitionRequired,
                tuitionPaid,
                functionalRequired,
                functionalPaid,
                overallStatus = OverallStatus(tuitionPaid + functionalPaid, tuitionRequired + functionalRequired),
                registration = new
                {
                    computedEligible = registrationComputed,
                    finalEligible = registrationCleared,
                    overridden = registrationOverride != null,
                    overrideReason = registrationOverride?.Reason
                },
                exam = new
                {
                    computedEligible = examComputed,
                    finalEligible = examCleared,
                    overridden = examOverride != null,
                    overrideReason = examOverride?.Reason
                }
            });
        }

       
        [HttpPost("student/{regNumber}/clearance/override")]
        public async Task<IActionResult> CreateClearanceOverride(
            string regNumber,
            [FromBody] CreateClearanceOverrideRequest request)
        {
            regNumber = System.Net.WebUtility.UrlDecode(regNumber);

            var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
            if (student == null)
                return NotFound(new { message = "Student not found" });

            if (request.ClearanceType != "REGISTRATION" && request.ClearanceType != "EXAM")
                return BadRequest(new { message = "ClearanceType must be 'REGISTRATION' or 'EXAM'" });

            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { message = "Reason is required for any clearance override" });

            if (request.Semester != 1 && request.Semester != 2)
                return BadRequest(new { message = "Semester must be 1 or 2" });

            var existing = await _context.ClearanceOverrides
                .Where(co => co.RegNumber == regNumber &&
                             co.ClearanceType == request.ClearanceType &&
                             co.AcademicYear == request.AcademicYear &&
                             co.Semester == request.Semester &&
                             co.IsActive)
                .ToListAsync();

            foreach (var old in existing)
                old.IsActive = false;

            var overrideEntry = new ClearanceOverride
            {
                RegNumber = regNumber,
                ClearanceType = request.ClearanceType,
                AcademicYear = request.AcademicYear,
                Semester = request.Semester,
                IsGranted = request.IsGranted,
                Reason = request.Reason,
                BursarId = GetCurrentBursarId(),
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.ClearanceOverrides.Add(overrideEntry);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync(
                "CLEARANCE_OVERRIDE",
                $"{(request.IsGranted ? "Granted" : "Revoked")} {request.ClearanceType} clearance for {regNumber} ({request.AcademicYear} Sem {request.Semester}). Reason: {request.Reason}",
                bursarId: GetCurrentBursarId(),
                regNumber: regNumber,
                entityType: "ClearanceOverride",
                entityId: overrideEntry.OverrideId);

            return Ok(new
            {
                message = $"Clearance override recorded: {request.ClearanceType} {(request.IsGranted ? "granted" : "revoked")} for {regNumber}",
                overrideId = overrideEntry.OverrideId
            });
        }
       
        [HttpGet("reports/daily-collections")]
        public async Task<IActionResult> GetDailyCollections([FromQuery] DateTime? date)
        {
            var targetDate = (date ?? DateTime.Today).Date;

            var payments = await _context.Payments
                .Where(p => p.Status == "Approved" && p.PaymentDate.Date == targetDate)
                .Include(p => p.Student)
                .OrderBy(p => p.PaymentDate)
                .Select(p => new
                {
                    p.PaymentId,
                    p.RegNumber,
                    StudentName = p.Student != null ? $"{p.Student.FirstName} {p.Student.LastName}" : null,
                    p.PaymentCategory,
                    p.Amount,
                    p.PaymentMethod,
                    p.TransactionReference,
                    p.PaymentDate
                })
                .ToListAsync();

            return Ok(new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                totalCollected = payments.Sum(p => p.Amount),
                transactionCount = payments.Count,
                payments
            });
        }

       
        [HttpGet("reports/outstanding-balances")]
        public async Task<IActionResult> GetOutstandingBalances(
            [FromQuery] string? programmeCode,
            [FromQuery] decimal minimumBalance = 0)
        {
            var query = _context.Students
                .Where(s => (s.FeesBalance ?? 0) > minimumBalance);

            if (!string.IsNullOrWhiteSpace(programmeCode))
                query = query.Where(s => s.ProgrammeCode == programmeCode);

            var students = await query
                .OrderByDescending(s => s.FeesBalance)
                .Select(s => new
                {
                    s.RegNumber,
                    StudentName = $"{s.FirstName} {s.LastName}",
                    s.ProgrammeCode,
                    s.ProgrammeName,
                    s.CurrentYear,
                    s.FeesBalance,
                    s.TotalPaid
                })
                .ToListAsync();

            return Ok(new
            {
                minimumBalance,
                studentCount = students.Count,
                totalOutstanding = students.Sum(s => s.FeesBalance ?? 0),
                students
            });
        }

      
        [HttpGet("reports/defaulters")]
        public async Task<IActionResult> GetDefaulters([FromQuery] decimal threshold = 0)
        {
            var defaulters = await _context.Students
                .Where(s => (s.FeesBalance ?? 0) > threshold)
                .OrderByDescending(s => s.FeesBalance)
                .Select(s => new
                {
                    s.RegNumber,
                    StudentName = $"{s.FirstName} {s.LastName}",
                    s.ProgrammeCode,
                    s.CurrentYear,
                    s.FeesBalance,
                    s.Status
                })
                .ToListAsync();

            return Ok(new
            {
                threshold,
                defaulterCount = defaulters.Count,
                totalOutstanding = defaulters.Sum(s => s.FeesBalance ?? 0),
                defaulters
            });
        }

        
        [HttpGet("payment-verification/pending")]
        public async Task<IActionResult> GetPendingVerifications()
        {
            var pending = await _context.Payments
                .Where(p => p.Status == "Pending")
                .Include(p => p.Student)
                .OrderBy(p => p.PaymentDate)
                .Select(p => new
                {
                    p.PaymentId,
                    p.RegNumber,
                    StudentName = p.Student != null ? $"{p.Student.FirstName} {p.Student.LastName}" : null,
                    p.PaymentCategory,
                    p.Amount,
                    p.PaymentMethod,
                    p.TransactionReference,
                    p.PaymentProofPath,
                    p.PaymentDate,
                    p.Description
                })
                .ToListAsync();

            return Ok(new { pendingCount = pending.Count, pending });
        }

       

        
        [HttpGet("admission-fee/pending")]
        public async Task<IActionResult> GetPendingAdmissionFeePayments()
        {
            try
            {
                var pending = await _context.Applications
                    .Where(a => a.PaymentStatus == "PENDING_VERIFICATION")
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new
                    {
                        a.ApplicationId,
                        a.ApplicationNumber,
                        a.FirstName,
                        a.LastName,
                        a.Email,
                        a.ProgrammeName,
                        a.PaymentStatus,
                        a.PaymentAmount,
                        a.CreatedAt,
                        a.PaymentProofPath
                    })
                    .ToListAsync();

                return Ok(new { success = true, count = pending.Count, data = pending });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error loading pending admission fee payments", error = ex.Message });
            }
        }

       
        [HttpPost("admission-fee/verify")]
        public async Task<IActionResult> VerifyAdmissionFeePayment([FromBody] AdmissionFeeVerificationRequest request)
        {
            try
            {
                if (request == null || request.ApplicationId <= 0)
                {
                    return BadRequest(new { success = false, message = "ApplicationId is required" });
                }

                var application = await _context.Applications
                    .FirstOrDefaultAsync(a => a.ApplicationId == request.ApplicationId);

                if (application == null)
                {
                    return NotFound(new { success = false, message = "Application not found" });
                }

                if (application.PaymentStatus == "VERIFIED")
                {
                    return BadRequest(new { success = false, message = "Payment already verified" });
                }

                var bursarId = GetCurrentBursarId();

                if (request.IsApproved)
                {
                    application.PaymentStatus = "VERIFIED";
                    application.ApplicationStatus = "PENDING_REVIEW";
                }
                else
                {
                    application.PaymentStatus = "REJECTED";
                    application.ApplicationStatus = "REJECTED";
                    application.AdminNotes = "PAYMENT REJECTION: " + (request.RejectionReason ?? "No reason provided");
                    application.AdminReviewDate = DateTime.Now;
                }

                application.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync(
                    request.IsApproved ? "ADMISSION_FEE_VERIFIED" : "ADMISSION_FEE_REJECTED",
                    $"{(request.IsApproved ? "Verified" : "Rejected")} admission fee payment for application #{application.ApplicationId} ({application.Email})",
                    bursarId: bursarId,
                    entityType: "Application",
                    entityId: application.ApplicationId);

                if (!string.IsNullOrWhiteSpace(application.Email))
                {
                    string statusMessage = request.IsApproved
                        ? "Payment Verified - Awaiting Academic Registrar Review"
                        : $"Payment Rejected - {request.RejectionReason}";

                   
                }

                return Ok(new
                {
                    success = true,
                    message = request.IsApproved ? "Admission fee payment verified successfully" : "Admission fee payment rejected"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error verifying admission fee payment", error = ex.Message });
            }
        }

        

       
        [HttpPost("payment-verification/{paymentId}/approve")]
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            try
            {
                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId);
                if (payment == null)
                    return NotFound(new { message = "Payment not found" });

                if (payment.Status != "Pending")
                    return BadRequest(new { message = $"Payment status is '{payment.Status}', not 'Pending'. Only pending payments can be approved." });

                var bursarId = GetCurrentBursarId();

                payment.Status = "Approved";
                payment.ReviewedByBursarId = bursarId;
                payment.ReviewedAt = DateTime.Now;
                payment.ProcessedByBursarId = bursarId;
                payment.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                if (_finalizationService == null)
                    return StatusCode(500, new { message = "Payment finalization service is not available." });

                var result = await _finalizationService.FinalizeApprovedPaymentAsync(paymentId);

                await _auditLog.LogAsync(
                    "PAYMENT_APPROVED",
                    $"Approved {payment.PaymentCategory} payment of UGX {payment.Amount:N0} for {payment.RegNumber} (Ref: {payment.TransactionReference})",
                    bursarId: bursarId,
                    regNumber: payment.RegNumber,
                    entityType: "Payment",
                    entityId: payment.PaymentId);

                return Ok(new
                {
                    success = true,
                    message = "Payment approved and processed successfully",
                    result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error approving payment: {ex.Message}" });
            }
        }

        
        [HttpPost("payment-verification/{paymentId}/reject")]
        public async Task<IActionResult> RejectPayment(int paymentId, [FromBody] RejectPaymentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Reason))
                return BadRequest(new { message = "Reason is required to reject a payment" });

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId);
            if (payment == null)
                return NotFound(new { message = "Payment not found" });

            if (payment.Status != "Pending")
                return BadRequest(new { message = $"Payment status is '{payment.Status}', not 'Pending'. Only pending payments can be rejected." });

            payment.Status = "Rejected";
            payment.RejectionReason = request.Reason;
            payment.ReviewedByBursarId = GetCurrentBursarId();
            payment.ReviewedAt = DateTime.Now;
            payment.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await _auditLog.LogAsync(
                 "PAYMENT_REJECTED",
                 $"Rejected {payment.PaymentCategory} payment of UGX {payment.Amount:N0} for {payment.RegNumber}. Reason: {request.Reason}",
                 bursarId: GetCurrentBursarId(),
                 regNumber: payment.RegNumber,
                 entityType: "Payment",
                 entityId: payment.PaymentId);

            return Ok(new { success = true, message = "Payment rejected", paymentId, reason = request.Reason });
        }

        [HttpGet("reports/revenue-by-programme")]
        public async Task<IActionResult> GetRevenueByProgramme(
            [FromQuery] string? academicYear,
            [FromQuery] int? semester)
        {
            var paymentsQuery = _context.Payments
                .Where(p => p.Status == "Approved")
                .Join(_context.Students, p => p.RegNumber, s => s.RegNumber, (p, s) => new { Payment = p, Student = s });

            if (semester.HasValue)
                paymentsQuery = paymentsQuery.Where(x =>
                    _context.AcademicSemesters.Any(sem => sem.SemesterId == x.Payment.SemesterId && sem.Semester == semester.Value));

            if (!string.IsNullOrWhiteSpace(academicYear))
                paymentsQuery = paymentsQuery.Where(x =>
                    _context.AcademicSemesters.Any(sem => sem.SemesterId == x.Payment.SemesterId && sem.AcademicYear == academicYear));

            var grouped = await paymentsQuery
                .GroupBy(x => new { x.Student.ProgrammeCode, x.Student.ProgrammeName })
                .Select(g => new
                {
                    programmeCode = g.Key.ProgrammeCode,
                    programmeName = g.Key.ProgrammeName,
                    totalRevenue = g.Sum(x => x.Payment.Amount),
                    paymentCount = g.Count()
                })
                .OrderByDescending(g => g.totalRevenue)
                .ToListAsync();

            return Ok(new
            {
                academicYear,
                semester,
                grandTotal = grouped.Sum(g => g.totalRevenue),
                programmes = grouped
            });
        }

       
        [HttpGet("audit-log")]
        public async Task<IActionResult> GetAuditLog(
            [FromQuery] int? bursarId,
            [FromQuery] string? regNumber,
            [FromQuery] string? actionType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            var query = _context.AuditLogs
                .Where(a => a.BursarId != null) // exclude legacy Admin-only rows from this view
                .AsQueryable();

            if (bursarId.HasValue)
                query = query.Where(a => a.BursarId == bursarId.Value);

            if (!string.IsNullOrWhiteSpace(regNumber))
                query = query.Where(a => a.RegNumber == regNumber);

            if (!string.IsNullOrWhiteSpace(actionType))
                query = query.Where(a => a.Action == actionType);

            if (fromDate.HasValue)
                query = query.Where(a => a.ActionDate >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(a => a.ActionDate < toDate.Value.Date.AddDays(1));

            var results = await query
                .OrderByDescending(a => a.ActionDate)
                .Select(a => new
                {
                    a.AuditLogId,
                    a.Action,
                    a.Details,
                    a.BursarId,
                    BursarName = a.Bursar != null ? $"{a.Bursar.FirstName} {a.Bursar.LastName}" : null,
                    a.RegNumber,
                    a.EntityType,
                    a.EntityId,
                    a.ActionDate,
                    a.IPAddress
                })
                .Take(500) 
                .ToListAsync();

            return Ok(new { count = results.Count, entries = results });
        }

       
        [HttpGet("students/search")]
        
        public async Task<IActionResult> SearchStudents(
            [FromQuery] string? query,
            [FromQuery] string? programmeCode,
            [FromQuery] int? facultyId,
            [FromQuery] int? currentYear,
            [FromQuery] string? intakeYear, 
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var studentsQuery = _context.Students.AsQueryable();

            
            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                studentsQuery = studentsQuery.Where(s =>
                    (s.RegNumber != null && s.RegNumber.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (s.FirstName != null && s.FirstName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (s.LastName != null && s.LastName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (s.Email != null && s.Email.Contains(q, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(programmeCode))
                studentsQuery = studentsQuery.Where(s => s.ProgrammeCode == programmeCode);

            if (currentYear.HasValue)
                studentsQuery = studentsQuery.Where(s => s.CurrentYear == currentYear.Value);

            if (!string.IsNullOrWhiteSpace(intakeYear))
                studentsQuery = studentsQuery.Where(s => s.RegNumber != null && s.RegNumber.StartsWith(intakeYear + "/"));

            if (facultyId.HasValue)
            {
                
                var programmeCodesInFaculty = _context.Programmes
                    .Where(p => p.SchoolId != null &&
                        _context.Schools.Any(sc => sc.SchoolId == p.SchoolId && sc.FacultyId == facultyId.Value))
                    .Select(p => p.ProgrammeCode);

                studentsQuery = studentsQuery.Where(s => programmeCodesInFaculty.Contains(s.ProgrammeCode));
            }

            var totalCount = await studentsQuery.CountAsync();

            var students = await studentsQuery
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.RegNumber,
                    StudentName = $"{s.FirstName} {s.LastName}",
                    s.Email,
                    s.ProgrammeCode,
                    s.ProgrammeName,
                    s.CurrentYear,
                    s.Status,
                    s.FeesBalance,
                    s.TotalPaid
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                students
            });
        }

       
        [HttpGet("documents")]
        public async Task<IActionResult> GetDocuments(
            [FromQuery] string? regNumber,
            [FromQuery] string? documentType,
            [FromQuery] bool? feePaid,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            var query = _context.Documents.AsQueryable();

            if (!string.IsNullOrWhiteSpace(regNumber))
                query = query.Where(d => d.RegNumber == regNumber);

            if (!string.IsNullOrWhiteSpace(documentType))
                query = query.Where(d => d.DocumentType == documentType);

            if (feePaid.HasValue)
                query = query.Where(d => d.FeePaid == feePaid.Value);

            if (fromDate.HasValue)
                query = query.Where(d => d.DateGenerated >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(d => d.DateGenerated < toDate.Value.Date.AddDays(1));

            var documents = await query
                .Include(d => d.Student)
                .OrderByDescending(d => d.DateGenerated)
                .Select(d => new
                {
                    d.DocumentId,
                    d.RegNumber,
                    StudentName = d.Student != null ? $"{d.Student.FirstName} {d.Student.LastName}" : null,
                    d.DocumentType,
                    d.DocumentNumber,
                    d.Status,
                    d.FeePaid,
                    d.DateGenerated,
                    d.ExpiryDate,
                    d.QrVerificationCode,
                    d.DocumentPath
                })
                .Take(500)
                .ToListAsync();

            return Ok(new { count = documents.Count, documents });
        }


       
        [HttpGet("documents/verify/{code}")]
        public async Task<IActionResult> VerifyDocument(string code)
        {
            code = System.Net.WebUtility.UrlDecode(code).Trim();

            var document = await _context.Documents
                .Include(d => d.Student)
                .FirstOrDefaultAsync(d =>
                    d.VerificationId == code ||
                    d.QrVerificationCode == code ||
                    d.DocumentNumber == code);

            if (document == null)
                return NotFound(new { valid = false, message = "No document found with this verification code." });

            if (document.IsRevoked)
            {
                return Ok(new
                {
                    valid = false,
                    revoked = true,
                    message = "This document has been revoked and is no longer valid.",
                    revokedAt = document.RevokedAt
                });
            }

            bool isExpired = document.ExpiryDate.HasValue && document.ExpiryDate.Value < DateTime.Now;

            
            document.VerificationCount += 1;
            document.LastVerifiedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                valid = true,
                isExpired,
                document.DocumentType,
                document.DocumentNumber,
                document.Status,
                document.RegNumber,
                studentName = document.Student != null ? $"{document.Student.FirstName} {document.Student.LastName}" : null,
                document.DateGenerated,
                document.ExpiryDate,
                document.VerificationCount
            });
        }

        
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] decimal largeBalanceThreshold = 1000000)
        {
            var notifications = new List<object>();

            // 1. Pending payment verifications
            var pendingCount = await _context.Payments.CountAsync(p => p.Status == "Pending");
            if (pendingCount > 0)
            {
                notifications.Add(new
                {
                    type = "PENDING_VERIFICATIONS",
                    severity = "action_required",
                    message = $"{pendingCount} payment{(pendingCount == 1 ? "" : "s")} awaiting verification",
                    count = pendingCount
                });
            }

            
            var largeBalanceStudents = await _context.Students
                .Where(s => (s.FeesBalance ?? 0) >= largeBalanceThreshold)
                .CountAsync();
            if (largeBalanceStudents > 0)
            {
                notifications.Add(new
                {
                    type = "LARGE_OUTSTANDING_BALANCES",
                    severity = "info",
                    message = $"{largeBalanceStudents} student{(largeBalanceStudents == 1 ? "" : "s")} owing UGX {largeBalanceThreshold:N0} or more",
                    count = largeBalanceStudents,
                    threshold = largeBalanceThreshold
                });
            }

           
            var recentRejections = await _context.Payments
                .CountAsync(p => p.Status == "Rejected" && p.ReviewedAt >= DateTime.Now.AddDays(-7));
            if (recentRejections > 0)
            {
                notifications.Add(new
                {
                    type = "RECENT_REJECTIONS",
                    severity = "info",
                    message = $"{recentRejections} payment{(recentRejections == 1 ? "" : "s")} rejected in the last 7 days",
                    count = recentRejections
                });
            }

           
            var activeOverrides = await _context.ClearanceOverrides.CountAsync(co => co.IsActive);
            if (activeOverrides > 0)
            {
                notifications.Add(new
                {
                    type = "ACTIVE_CLEARANCE_OVERRIDES",
                    severity = "info",
                    message = $"{activeOverrides} active clearance override{(activeOverrides == 1 ? "" : "s")} in effect",
                    count = activeOverrides
                });
            }

           
            var todaysApprovals = await _context.Payments
                .CountAsync(p => p.Status == "Approved" && p.PaymentDate.Date == DateTime.Today);
            var todaysTotal = await _context.Payments
                .Where(p => p.Status == "Approved" && p.PaymentDate.Date == DateTime.Today)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
            if (todaysApprovals > 0)
            {
                notifications.Add(new
                {
                    type = "TODAYS_COLLECTIONS",
                    severity = "success",
                    message = $"UGX {todaysTotal:N0} collected today across {todaysApprovals} payment{(todaysApprovals == 1 ? "" : "s")}",
                    count = todaysApprovals,
                    amount = todaysTotal
                });
            }

            return Ok(new
            {
                generatedAt = DateTime.Now,
                notificationCount = notifications.Count,
                notifications
            });
        }
    }
}
public class CreateFeeStructureRequest
{
    public string ProgrammeCode { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public int Semester { get; set; }
    public decimal Tuition { get; set; }
    public decimal FunctionalFees { get; set; }
    public decimal OtherCharges { get; set; } = 0;
}

public class UpdateFeeStructureRequest
{
    public string? AcademicYear { get; set; }
    public int? Semester { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateClearanceOverrideRequest
{
    public string ClearanceType { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public int Semester { get; set; }
    public bool IsGranted { get; set; } = true;
    public string Reason { get; set; } = string.Empty;
}
public class RejectPaymentRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class AdmissionFeeVerificationRequest
{
    public int ApplicationId { get; set; }
    public bool IsApproved { get; set; }
    public string? RejectionReason { get; set; }

}