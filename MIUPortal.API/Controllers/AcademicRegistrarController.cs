using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/academic-registrar")]
    [Authorize(Roles = "AcademicRegistrar")] 
    public class AcademicRegistrarController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly AdmissionLetterService _admissionLetterService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AcademicRegistrarController> _logger;
        private readonly IConfiguration _configuration;
        private readonly AuditLogService _auditLog;
        private readonly SemesterProgressionService _semesterProgressionService;
        private readonly StudentPromotionService _promotionService;

        public AcademicRegistrarController(
            MIUContext context,
            AdmissionLetterService admissionLetterService,
            IEmailService emailService,
            ILogger<AcademicRegistrarController> logger,
            IConfiguration configuration,
            AuditLogService auditLog,
            SemesterProgressionService semesterProgressionService,
            StudentPromotionService promotionService)
        {
            _context = context;
            _admissionLetterService = admissionLetterService;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
            _auditLog = auditLog;
            _semesterProgressionService = semesterProgressionService;
            _promotionService = promotionService;
        }
        private int? GetCurrentRegistrarId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        // =========================================================
        // LOGIN
        // =========================================================
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> RegistrarLogin([FromBody] AcademicRegistrarLoginRequest? request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { success = false, message = "Email and password are required" });
                }

                var registrar = await _context.AcademicRegistrars
                    .FirstOrDefaultAsync(r => r.Email == request.Email.Trim().ToLower());

                if (registrar == null)
                {
                    return Unauthorized(new { success = false, message = "Invalid email or password" });
                }

                bool validPassword = BCrypt.Net.BCrypt.Verify(request.Password, registrar.PasswordHash);

                if (!validPassword)
                {
                    return Unauthorized(new { success = false, message = "Invalid email or password" });
                }

                if (registrar.Status != "Active")
                {
                    return Unauthorized(new { success = false, message = "Academic Registrar account is inactive" });
                }

                string token = GenerateJwtToken(registrar.AcademicRegistrarId, registrar.Email, "AcademicRegistrar");

                registrar.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Login successful",
                    token,
                    user = new
                    {
                        academicRegistrarId = registrar.AcademicRegistrarId,
                        email = registrar.Email,
                        firstName = registrar.FirstName,
                        lastName = registrar.LastName,
                        type = "academic_registrar"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Academic Registrar login error");
                return StatusCode(500, new { success = false, message = "Login failed", error = ex.Message });
            }
        }

        // =========================================================
        // SEMESTER ACTIVATION
        // =========================================================
        [HttpPost("semesters/activate")]
        public async Task<IActionResult> ActivateSemester([FromBody] ActivateSemesterRequest? request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.AcademicYear) || (request.Semester != 1 && request.Semester != 2))
                    return BadRequest(new { success = false, message = "AcademicYear and a valid Semester (1 or 2) are required" });

                if (request.StartDate >= request.EndDate)
                    return BadRequest(new { success = false, message = "StartDate must be before EndDate" });

                var result = await _semesterProgressionService.ActivateSemesterAsync(
                    request.AcademicYear, request.Semester, request.StartDate, request.EndDate);

                await _auditLog.LogAsync(
       "SEMESTER_ACTIVATED",
       $"Activated {request.AcademicYear} Semester {request.Semester} ({request.StartDate:d} - {request.EndDate:d}). Promoted {result.StudentsPromoted} previously-ready student(s).",
       academicRegistrarId: GetCurrentRegistrarId(),
       entityType: "AcademicSemester",
       entityId: result.Semester.SemesterId);

                return Ok(new
                {
                    success = true,
                    message = $"Semester activated. {result.StudentsPromoted} previously-ready student(s) promoted.",
                    data = result.Semester,
                    studentsPromoted = result.StudentsPromoted
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating semester");
                return StatusCode(500, new { success = false, message = "Error activating semester", error = ex.Message });
            }
        }

        [HttpGet("semesters")]
        public async Task<IActionResult> GetAllSemesters()
        {
            var semesters = await _context.AcademicSemesters.OrderByDescending(s => s.StartDate).ToListAsync();
            return Ok(new { success = true, data = semesters });
        }

        
        [HttpGet("students/active-count")]
        public async Task<IActionResult> GetActiveStudentCount()
        {
            int count = await _context.Students.CountAsync(s => s.Status == "ACTIVE");
            return Ok(new { success = true, count });
        }

        
        [HttpPost("system/backfill-enrollments")]
        public async Task<IActionResult> BackfillEnrollments()
        {
            try
            {
                int seeded = await _promotionService.BackfillMissingEnrollmentsAsync();
                return Ok(new { success = true, message = $"Seeded {seeded} enrollment row(s)." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running enrollment backfill");
                return StatusCode(500, new { success = false, message = "Error running enrollment backfill", error = ex.Message });
            }
        }

       
        [HttpPost("system/check-promotion")]
        public async Task<IActionResult> CheckPromotion([FromQuery] string regNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(regNumber))
                {
                    return BadRequest(new { success = false, message = "regNumber query parameter is required." });
                }

                var enrollment = await _promotionService.GetCurrentEnrollmentAsync(regNumber);
                if (enrollment == null)
                {
                    return NotFound(new { success = false, message = $"No current enrollment row found for {regNumber}. Run backfill-enrollments first." });
                }

                bool promoted = await _promotionService.PromoteStudentIfCompleteAsync(
                    regNumber, enrollment.PersonalYear, enrollment.PersonalSemester);

                return Ok(new
                {
                    success = true,
                    checkedPeriod = new { enrollment.PersonalYear, enrollment.PersonalSemester },
                    promoted,
                    message = promoted
                        ? "Student was promoted (or marked ready) — check studentsemesterenrollments / ReadyForPromotion."
                        : "Student was NOT promoted — period is not yet fully complete (some course missing or not RegistrarSignedOff)."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking promotion for {RegNumber}", regNumber);
                return StatusCode(500, new { success = false, message = "Error checking promotion", error = ex.Message });
            }
        }

        // =========================================================
        // DASHBOARD STATS
        // =========================================================
        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                int pendingReview = await _context.Applications
                    .CountAsync(a => a.ApplicationStatus == "PENDING_REVIEW" && a.PaymentStatus == "VERIFIED");

                int approvedTotal = await _context.Applications.CountAsync(a => a.ApplicationStatus == "APPROVED");
                int rejectedTotal = await _context.Applications.CountAsync(a => a.ApplicationStatus == "REJECTED");

                int pendingSignoff = await _context.Results.CountAsync(r => r.Status == "DeanApproved");
                int signedOffTotal = await _context.Results.CountAsync(r => r.Status == "RegistrarSignedOff");

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        pendingReview,
                        approvedTotal,
                        rejectedTotal,
                        pendingSignoff,
                        signedOffTotal
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading registrar dashboard stats");
                return StatusCode(500, new { success = false, message = "Error loading dashboard stats", error = ex.Message });
            }
        }

        
        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications([FromQuery] string? status)
        {
            try
            {
                var query = _context.Applications.AsQueryable();

                if (!string.IsNullOrWhiteSpace(status) && status.ToUpper() != "ALL")
                {
                    query = query.Where(a => a.ApplicationStatus == status.ToUpper());
                }

                var applications = await query
                    .Select(a => new
                    {
                        a.ApplicationId,
                        a.ApplicationNumber,
                        a.FirstName,
                        a.LastName,
                        a.Email,
                        a.ProgrammeName,
                        a.ApplicationStatus,
                        a.PaymentStatus,
                        a.PaymentAmount,
                        a.RegistrationNumber,
                        a.CreatedAt,
                        a.ApprovalDate
                    })
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();

                return Ok(new { success = true, count = applications.Count, data = applications });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching applications for registrar");
                return StatusCode(500, new { success = false, message = "Error fetching applications", error = ex.Message });
            }
        }

        [HttpGet("application/{applicationId}")]
        public async Task<IActionResult> GetApplicationDetails(int applicationId)
        {
            try
            {
                var application = await _context.Applications
                    .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

                if (application == null)
                {
                    return NotFound(new { success = false, message = "Application not found" });
                }

                return Ok(new { success = true, data = application });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching application details for registrar");
                return StatusCode(500, new { success = false, message = "Error fetching application details", error = ex.Message });
            }
        }



        // =========================================================
        // APPLICATION APPROVAL + ADMISSION LETTER GENERATION
        // =========================================================
        [HttpPost("approve-application")]
        public async Task<IActionResult> ApproveApplication([FromBody] ApproveApplicationRequest? request)
        {
            try
            {
                if (request == null || request.ApplicationId <= 0)
                {
                    return BadRequest(new { success = false, message = "ApplicationId is required" });
                }

                var strategy = _context.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        var application = await _context.Applications
                            .FirstOrDefaultAsync(a => a.ApplicationId == request.ApplicationId);

                        if (application == null)
                        {
                            return NotFound(new { success = false, message = "Application not found" });
                        }

                        if (application.ApplicationStatus == "APPROVED")
                        {
                            return BadRequest(new { success = false, message = "Application already approved" });
                        }

                        if (application.PaymentStatus != "VERIFIED")
                        {
                            return BadRequest(new { success = false, message = "Payment must be verified first" });
                        }

                        string regNumber = await GenerateRegistrationNumberAsync(
                            application.ProgrammeCode ?? "GEN",
                            application.CampusPreference ?? "UMC"
                        );

                        bool studentExists = await _context.Students.AnyAsync(s => s.RegNumber == regNumber);
                        if (application.PaymentStatus != "VERIFIED")
                        {
                            return BadRequest(new { success = false, message = "Payment must be verified first" });
                        }

                       
                        if (application.ApplicationCategory == "GovernmentLoan" && application.LoanSchemeStatus != "Approved")
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = application.LoanSchemeStatus == "Rejected"
                                    ? "This applicant's Government Loan Scheme was rejected. They must switch category or reapply before admission can proceed."
                                    : "This applicant's Government Loan Scheme proof is still awaiting review. Review it before approving admission."
                            });
                        }

                        if (studentExists)
                        {
                            return BadRequest(new { success = false, message = "Registration number already exists" });
                        }

                       
                        string resolvedCampusCode = string.IsNullOrWhiteSpace(application.CampusPreference) ? "UMC" : application.CampusPreference;
                        var campusRecord = await _context.Campuses.FirstOrDefaultAsync(c => c.CampusCode == resolvedCampusCode);

                        var student = new Student
                        {
                            RegNumber = regNumber,
                            FirstName = application.FirstName ?? "Student",
                            MiddleName = application.MiddleName,
                            LastName = application.LastName ?? "Applicant",
                            Email = application.Email ?? "",
                            PasswordHash = application.PasswordHash ?? "",
                            DateOfBirth = application.DateOfBirth,
                            Gender = application.Gender,
                            Nationality = application.Nationality,
                            NationalId = application.NationalId,
                            Religion = application.Religion,
                            MaritalStatus = application.MaritalStatus,
                            PhoneNumber = string.IsNullOrWhiteSpace(application.PrimaryPhone) ? "N/A" : application.PrimaryPhone,
                            ProgrammeCode = application.ProgrammeCode,
                            ProgrammeName = application.ProgrammeName,
                            IntakeMonth = application.Intake,
                            StudyMode = application.StudyMode,
                            CampusCode = resolvedCampusCode,
                            Campus = campusRecord?.CampusName ?? resolvedCampusCode,
                            PassportPhotoPath = application.PassportPhotoPath,
                            Status = "ACTIVE",
                            DateAdmitted = DateTime.Now,
                            CurrentYear = 1,
                            CurrentSemester = 1,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            FeeCategory = application.ApplicationCategory,
                            LoanSchemeApproved = application.ApplicationCategory == "GovernmentLoan" && application.LoanSchemeStatus == "Approved"
                        };

                        _context.Students.Add(student);
                        await _context.SaveChangesAsync();

                        var activeSemester = await _semesterProgressionService.GetActiveSemesterAsync();

                       
                        if (activeSemester != null)
                        {
                            _context.StudentSemesterEnrollments.Add(new StudentSemesterEnrollment
                            {
                                RegNumber = student.RegNumber!,
                                SemesterId = activeSemester.SemesterId,
                                PersonalYear = 1,
                                PersonalSemester = 1,
                                IsCurrent = true,
                                PromotedAt = DateTime.Now,
                                PromotedBy = "Admission"
                            });
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            
                            _logger.LogWarning(
                                "No active semester found — student {Reg} admitted without an initial enrollment row.",
                                student.RegNumber);
                        }

                        string pdfPath = await _admissionLetterService.GenerateAdmissionLetterPdf(application, student, activeSemester);

                        application.ApplicationStatus = "APPROVED";
                        application.RegistrationNumber = regNumber;
                        application.ApprovalDate = DateTime.Now;
                        application.AdmissionLetterPath = pdfPath;
                        application.ApprovedByRegistrarId = GetCurrentRegistrarId();
                        application.UpdatedAt = DateTime.Now;

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        if (!string.IsNullOrWhiteSpace(application.Email))
                        {
                            await _emailService.SendAdmissionLetterEmailAsync(
                                application.Email,
                                application.FirstName ?? "Student",
                                application.LastName ?? "Applicant",
                                regNumber,
                                pdfPath
                            );
                        }

                        _logger.LogInformation("Application approved by Academic Registrar: {Email}", application.Email);

                        await _auditLog.LogAsync(
                            "APPLICATION_APPROVED",
                            $"Approved application #{application.ApplicationId} ({application.Email}), assigned reg number {regNumber}",
                            academicRegistrarId: GetCurrentRegistrarId(),
                            entityType: "Application",
                            entityId: application.ApplicationId);

                        return Ok(new
                        {
                            success = true,
                            message = "Application approved successfully",
                            data = new
                            {
                                applicationId = application.ApplicationId,
                                registrationNumber = regNumber,
                                studentId = student.StudentId,
                                studentEmail = application.Email,
                                approvalDate = application.ApprovalDate
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error approving application");
                        return StatusCode(500, new { success = false, message = "Error approving application", error = ex.Message });
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error approving application");
                return StatusCode(500, new { success = false, message = "Fatal error approving application", error = ex.Message });
            }
        }

        // =============================
        // APPLICATION REJECTION 
        // =============================
        [HttpPost("reject-application")]
        public async Task<IActionResult> RejectApplication([FromBody] RejectApplicationRequest? request)
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

                if (application.ApplicationStatus == "REJECTED")
                {
                    return BadRequest(new { success = false, message = "Application already rejected" });
                }

                if (application.ApplicationStatus == "APPROVED")
                {
                    return BadRequest(new { success = false, message = "Approved applications cannot be rejected" });
                }

                application.ApplicationStatus = "REJECTED";
                application.AdminNotes = request.RejectionReason;
                application.AdminReviewDate = DateTime.Now;
                application.ApprovedByRegistrarId = GetCurrentRegistrarId();
                application.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(application.Email))
                {
                    string statusMessage = $"Application Rejected - {request.RejectionReason}";
                    await _emailService.SendApplicationStatusEmailAsync(
                        application.Email,
                        application.FirstName ?? "Student",
                        statusMessage
                    );
                }

                _logger.LogInformation("Application rejected by Academic Registrar: {ApplicationId}", application.ApplicationId);

                await _auditLog.LogAsync(
                    "APPLICATION_REJECTED",
                    $"Rejected application #{application.ApplicationId} ({application.Email}). Reason: {request.RejectionReason}",
                    academicRegistrarId: GetCurrentRegistrarId(),
                    entityType: "Application",
                    entityId: application.ApplicationId);

                return Ok(new { success = true, message = "Application rejected successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting application");
                return StatusCode(500, new { success = false, message = "Error rejecting application", error = ex.Message });
            }
        }

        // =========================================================
        // GOVERNMENT LOAN SCHEME REVIEW
        // =========================================================
        [HttpGet("loan-applications")]
        public async Task<IActionResult> GetLoanApplications([FromQuery] string? status)
        {
            try
            {
                var query = _context.Applications
                    .Where(a => a.ApplicationCategory == "GovernmentLoan");

                if (!string.IsNullOrWhiteSpace(status) && status.ToUpper() != "ALL")
                {
                    query = query.Where(a => a.LoanSchemeStatus == status);
                }

                var applications = await query
                    .Select(a => new
                    {
                        a.ApplicationId,
                        a.ApplicationNumber,
                        a.FirstName,
                        a.LastName,
                        a.Email,
                        a.ProgrammeName,
                        a.LoanProofPath,
                        a.LoanSchemeStatus,
                        a.LoanReviewedBy,
                        a.LoanReviewedDate,
                        a.LoanRejectionReason,
                        a.PaymentStatus,
                        a.ApplicationStatus,
                        a.CreatedAt
                    })
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();

                return Ok(new { success = true, count = applications.Count, data = applications });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching loan applications");
                return StatusCode(500, new { success = false, message = "Error fetching loan applications", error = ex.Message });
            }
        }

        [HttpPost("loan-applications/{applicationId}/review")]
        public async Task<IActionResult> ReviewLoanApplication(int applicationId, [FromBody] LoanReviewRequest request)
        {
            try
            {
                var application = await _context.Applications
                    .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

                if (application == null)
                    return NotFound(new { success = false, message = "Application not found" });

                if (application.ApplicationCategory != "GovernmentLoan")
                    return BadRequest(new { success = false, message = "This application is not on the Government Loan Scheme category." });

                if (string.IsNullOrWhiteSpace(application.LoanProofPath))
                    return BadRequest(new { success = false, message = "Student has not yet uploaded proof of the loan scheme." });

                if (application.LoanSchemeStatus == "Approved" || application.LoanSchemeStatus == "Rejected")
                    return BadRequest(new { success = false, message = $"Loan scheme review already finalized as '{application.LoanSchemeStatus}'." });

                if (request.IsApproved && string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    
                }
                else if (!request.IsApproved && string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    return BadRequest(new { success = false, message = "RejectionReason is required when rejecting a loan scheme application." });
                }

                application.LoanSchemeStatus = request.IsApproved ? "Approved" : "Rejected";
                application.LoanReviewedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "AcademicRegistrar";
                application.LoanReviewedDate = DateTime.Now;
                application.LoanRejectionReason = request.IsApproved ? null : request.RejectionReason;
                application.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                await _auditLog.LogAsync(
                    request.IsApproved ? "LOAN_SCHEME_APPROVED" : "LOAN_SCHEME_REJECTED",
                    $"{(request.IsApproved ? "Approved" : "Rejected")} Government Loan Scheme for application #{application.ApplicationId} ({application.Email})" +
                        (request.IsApproved ? "" : $". Reason: {request.RejectionReason}"),
                    academicRegistrarId: GetCurrentRegistrarId(),
                    entityType: "Application",
                    entityId: application.ApplicationId);

                if (!string.IsNullOrWhiteSpace(application.Email))
                {
                    string statusMessage = request.IsApproved
                        ? "Government Loan Scheme Approved — Tuition Waived"
                        : $"Government Loan Scheme Rejected - {request.RejectionReason}";

                    await _emailService.SendApplicationStatusEmailAsync(
                        application.Email,
                        application.FirstName ?? "Student",
                        statusMessage
                    );
                }

                return Ok(new
                {
                    success = true,
                    message = request.IsApproved
                        ? "Loan scheme approved. Student will only owe functional fees once admitted."
                        : "Loan scheme rejected."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reviewing loan application");
                return StatusCode(500, new { success = false, message = "Error reviewing loan application", error = ex.Message });
            }
        }
        // =========================================================
        // RESULTS SIGN-OFF (final stage, after Dean approval)
        // =========================================================
        [HttpGet("results/pending-signoff")]
        public async Task<IActionResult> GetPendingSignoffResults()
        {
            try
            {
                var results = await _context.Results
                    .Where(r => r.Status == "DeanApproved")
                    .Include(r => r.Student)
                    .Include(r => r.Course)
                    .OrderByDescending(r => r.DeanApprovalDate)
                    .Select(r => new
                    {
                        r.ResultId,
                        regNumber = r.RegNumber,
                        studentName = r.Student != null ? $"{r.Student.FirstName} {r.Student.LastName}" : "N/A",
                        courseCode = r.CourseCode,
                        courseName = r.CourseName,
                        mark = r.Mark,
                        grade = r.Grade,
                        approvedByDeanId = r.ApprovedByDeanId,
                        deanApprovalDate = r.DeanApprovalDate
                    })
                    .ToListAsync();

                return Ok(new { success = true, count = results.Count, data = results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading results pending sign-off");
                return StatusCode(500, new { success = false, message = "Error loading results pending sign-off", error = ex.Message });
            }
        }

        [HttpPost("results/sign-off")]
        public async Task<IActionResult> SignOffResult([FromBody] ResultApprovalRequest request)
        {
            try
            {
                if (request == null || request.ResultId <= 0)
                {
                    return BadRequest(new { success = false, message = "ResultId is required" });
                }

                var result = await _context.Results
                    .Include(r => r.Student)
                    .FirstOrDefaultAsync(r => r.ResultId == request.ResultId);

                if (result == null)
                {
                    return NotFound(new { success = false, message = "Result not found" });
                }

                if (result.Status != "DeanApproved")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Result status is '{result.Status}'. Only Dean-approved results can be signed off."
                    });
                }

                result.Status = request.IsApproved ? "RegistrarSignedOff" : "Rejected";
                result.SignedOffByRegistrarId = GetCurrentRegistrarId();
                result.RegistrarSignOffDate = DateTime.Now;
                result.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();


                if (request.IsApproved && !string.IsNullOrWhiteSpace(result.RegNumber))
                {
                    try
                    {
                        await _promotionService.PromoteStudentIfCompleteAsync(
                            result.RegNumber, result.Year ?? 0, result.Semester ?? 0);
                    }
                    catch (Exception promotionEx)
                    {

                        _logger.LogWarning(promotionEx,
                            "Promotion check failed for {Reg} after signing off Result {ResultId}",
                            result.RegNumber, result.ResultId);
                    }
                }

                await _auditLog.LogAsync(
                    request.IsApproved ? "RESULT_SIGNED_OFF" : "RESULT_REJECTED_AT_SIGNOFF",
                    $"{(request.IsApproved ? "Signed off" : "Rejected")} result #{result.ResultId} ({result.CourseCode}) for {result.RegNumber}",
                    academicRegistrarId: GetCurrentRegistrarId(),
                    regNumber: result.RegNumber,
                    entityType: "Result",
                    entityId: result.ResultId);

               
                if (request.IsApproved && result.Student != null && !string.IsNullOrWhiteSpace(result.Student.Email))
                {
                    try
                    {
                        await _emailService.SendResultsPublishedEmailAsync(
                            result.Student.Email,
                            result.Student.FirstName ?? "Student",
                            result.CourseCode ?? "",
                            result.CourseName ?? "",
                            result.Grade ?? "N/A");
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogWarning(emailEx, "Failed to send results-published email for Result {ResultId}", result.ResultId);
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = request.IsApproved ? "Result signed off successfully" : "Result rejected at sign-off stage"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing off result");
                return StatusCode(500, new { success = false, message = "Error signing off result", error = ex.Message });
            }
        }

        // =========================================================
        // HELPERS
        // =========================================================
        private async Task<string> GenerateRegistrationNumberAsync(string programmeCode, string campus)
        {
            int year = DateTime.Now.Year % 100;
            string prefix = $"{year}/{programmeCode}/";

            var lastRegNumber = await _context.Students
                .Where(s => s.RegNumber != null && s.RegNumber.StartsWith(prefix))
                .OrderByDescending(s => s.RegNumber)
                .Select(s => s.RegNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;

            if (!string.IsNullOrWhiteSpace(lastRegNumber))
            {
                var parts = lastRegNumber.Split('/');
                if (parts.Length >= 3 && int.TryParse(parts[2], out int lastSequence))
                {
                    nextNumber = lastSequence + 1;
                }
            }

            return $"{year}/{programmeCode}/{nextNumber:D3}/{campus}";
        }

        private string GenerateJwtToken(int registrarId, string email, string role)
        {
            string secretKey = _configuration["JwtSettings:SecretKey"]
                ?? throw new Exception("JWT SecretKey missing");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, registrarId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // =========================================================
    // DTOs
    // =========================================================
    public class AcademicRegistrarLoginRequest
    {
        public string Email { get; set; } = "";
        public string? Password { get; set; }
    }

    public class ActivateSemesterRequest
    {
        public string AcademicYear { get; set; } = "";
        public int Semester { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
    public class LoanReviewRequest
    {
        public bool IsApproved { get; set; }
        public string? RejectionReason { get; set; }
    }
}