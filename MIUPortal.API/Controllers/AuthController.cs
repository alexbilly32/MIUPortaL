using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.DTOs; 
using MIUPortal.API.Models;
using MIUPortal.API.Services;
using MIUPortal.API.Utilities;
using System.Security.Cryptography;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(
        MIUContext context,
        IEmailService emailService,
        ILogger<AuthController> logger,
        AuditLogService auditLog,
        JwtService jwtService)
        : ControllerBase
    {
        private readonly MIUContext _context = context;
        private readonly IEmailService _emailService = emailService;
        private readonly ILogger<AuthController> _logger = logger;
        private readonly JwtService _jwtService = jwtService;
        private readonly AuditLogService _auditLog = auditLog;

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                success = true,
                message = "MIU Portal API running",
                timestamp = DateTime.UtcNow
            });
        }

      
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] StudentLoginRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { success = false, message = "Email and password are required" });
                }

                string email = request.Email.Trim().ToLower();

                // ---------- STUDENT ----------
                var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Email == email);
                if (student != null)
                {
                    if (!VerifyPassword(request.Password, student.PasswordHash ?? ""))
                    {
                        await _auditLog.LogAsync("LOGIN_FAILED", $"Failed login attempt (student email match): {email}");
                        return Unauthorized(new { success = false, message = "Invalid email or password." });
                    }

                    string token = _jwtService.GenerateJwt(student.Email ?? "", "Student", student.StudentId);
                    _logger.LogInformation("Student authenticated via unified login: {Email}", email);
                    await _auditLog.LogAsync("LOGIN_SUCCESS", $"Student login: {email}");

                    return Ok(new
                    {
                        success = true,
                        token,
                        role = "student",
                        redirectUrl = "student-dashboard.html",
                        user = new
                        {
                            regNumber = student.RegNumber,
                            email = student.Email,
                            firstName = student.FirstName,
                            lastName = student.LastName,
                            programme = student.ProgrammeName,
                            passportPhoto = student.PassportPhotoPath,
                            type = "student"
                        }
                    });
                }

                var application = await _context.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Email == email);
                if (application != null)
                {
                    if (!VerifyPassword(request.Password, application.PasswordHash ?? ""))
                    {
                        await _auditLog.LogAsync("LOGIN_FAILED", $"Failed login attempt (applicant email match): {email}");
                        return Unauthorized(new { success = false, message = "Invalid email or password." });
                    }

                    if (application.ApplicationStatus != "APPROVED")
                    {
                        await _auditLog.LogAsync("LOGIN_BLOCKED_PENDING", $"Applicant login blocked (status={application.ApplicationStatus}): {email}");
                        return Unauthorized(new
                        {
                            success = false,
                            message = "Your portal application status is pending review. Check back soon.",
                            status = application.ApplicationStatus
                        });
                    }

                    string token = _jwtService.GenerateJwt(application.Email ?? "", "Applicant", application.ApplicationId);
                    await _auditLog.LogAsync("LOGIN_SUCCESS", $"Applicant login: {email}");

                    return Ok(new
                    {
                        success = true,
                        token,
                        role = "applicant",
                        redirectUrl = "student-dashboard.html",
                        user = new
                        {
                            regNumber = application.RegistrationNumber,
                            email = application.Email,
                            firstName = application.FirstName,
                            lastName = application.LastName,
                            type = "applicant"
                        }
                    });
                }

               


                var lecturer = await _context.Lecturers
                    .Include(l => l.Faculty)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Email == email);

                if (lecturer != null)
                {
                    if (!VerifyPassword(request.Password, lecturer.PasswordHash ?? ""))
                    {
                        await _auditLog.LogAsync("LOGIN_FAILED", $"Failed login attempt (lecturer email match): {email}");
                        return Unauthorized(new { success = false, message = "Invalid email or password." });
                    }

                    if (lecturer.Status != "Active")
                    {
                        await _auditLog.LogAsync("LOGIN_BLOCKED_INACTIVE", $"Lecturer login blocked (inactive): {email}");
                        return Unauthorized(new { success = false, message = "Invalid credentials or inactive account" });
                    }

                    string token = _jwtService.GenerateLecturerJwt(lecturer);
                    await _auditLog.LogAsync("LOGIN_SUCCESS", $"Lecturer login: {email}");

                    if (lecturer.MustChangePassword)
                    {
                        return Ok(new
                        {
                            success = true,
                            token,
                            role = "lecturer",
                            mustChangePassword = true,
                            redirectUrl = "change-password.html",
                            user = new
                            {
                                email = lecturer.Email,
                                firstName = lecturer.FirstName,
                                lastName = lecturer.LastName
                            }
                        });
                    }

                    string lecturerRedirect = lecturer.IsDean == true ? "dean-dashboard.html" : "lecturer-dashboard.html";

                    return Ok(new
                    {
                        success = true,
                        token,
                        role = "lecturer",
                        mustChangePassword = false,
                        redirectUrl = lecturerRedirect,
                        user = new
                        {
                            lecturerId = lecturer.LecturerId,
                            email = lecturer.Email,
                            firstName = lecturer.FirstName,
                            lastName = lecturer.LastName,
                            fullName = $"{lecturer.FirstName} {lecturer.LastName}",
                            facultyId = lecturer.FacultyId,
                            facultyName = lecturer.Faculty?.FacultyName ?? "N/A",
                            phoneNumber = lecturer.PhoneNumber,
                            status = lecturer.Status,
                            isDean = lecturer.IsDean,
                            type = "lecturer"
                        }
                    });
                }

                

                var admin = await _context.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.Email == email);
                if (admin != null)
                {
                    if (!VerifyPassword(request.Password, admin.PasswordHash ?? ""))
                    {
                        await _auditLog.LogAsync("LOGIN_FAILED", $"Failed login attempt (admin email match): {email}");
                        return Unauthorized(new { success = false, message = "Invalid email or password." });
                    }

                    string token = _jwtService.GenerateJwt(admin.Email ?? "", "Admin", admin.AdminId);
                    await _auditLog.LogAsync("LOGIN_SUCCESS", $"Admin login: {email}");

                    return Ok(new
                    {
                        success = true,
                        token,
                        role = "admin",
                        redirectUrl = "admin-dashboard.html",
                        user = new
                        {
                            email = admin.Email,
                            firstName = admin.FirstName,
                            lastName = admin.LastName,
                            type = "admin"
                        }
                    });
                }

               

                var bursar = await _context.Bursars.AsNoTracking().FirstOrDefaultAsync(b => b.Email == email);
                if (bursar != null)
                {
                    if (!VerifyPassword(request.Password, bursar.PasswordHash))
                    {
                        await _auditLog.LogAsync("LOGIN_FAILED", $"Failed login attempt (bursar email match): {email}");
                        return Unauthorized(new { success = false, message = "Invalid email or password." });
                    }

                    if (bursar.Status != "Active")
                    {
                        await _auditLog.LogAsync("LOGIN_BLOCKED_INACTIVE", $"Bursar login blocked (inactive): {email}");
                        return Unauthorized(new
                        {
                            success = false,
                            message = "Your bursar account is not active. Contact the system administrator.",
                            status = bursar.Status
                        });
                    }

                    string token = _jwtService.GenerateJwt(bursar.Email, "Bursar", bursar.BursarId);
                    await _auditLog.LogAsync("LOGIN_SUCCESS", $"Bursar login: {email}");

                    return Ok(new
                    {
                        success = true,
                        token,
                        role = "bursar",
                        redirectUrl = "bursar-dashboard.html",
                        user = new
                        {
                            bursarId = bursar.BursarId,
                            email = bursar.Email,
                            firstName = bursar.FirstName,
                            lastName = bursar.LastName,
                            type = "bursar"
                        }
                    });
                }

                // ---------- ACADEMIC REGISTRAR ----------

                var registrar = await _context.AcademicRegistrars.AsNoTracking().FirstOrDefaultAsync(r => r.Email == email);
                if (registrar != null)
                {
                    if (!VerifyPassword(request.Password, registrar.PasswordHash))
                    {
                        await _auditLog.LogAsync("LOGIN_FAILED", $"Failed login attempt (registrar email match): {email}");
                        return Unauthorized(new { success = false, message = "Invalid email or password." });
                    }

                    if (registrar.Status != "Active")
                    {
                        await _auditLog.LogAsync("LOGIN_BLOCKED_INACTIVE", $"Registrar login blocked (inactive): {email}");
                        return Unauthorized(new { success = false, message = "Academic Registrar account is inactive" });
                    }

                    string token = _jwtService.GenerateJwt(registrar.Email, "AcademicRegistrar", registrar.AcademicRegistrarId);
                    await _auditLog.LogAsync("LOGIN_SUCCESS", $"Registrar login: {email}");

                    return Ok(new
                    {
                        success = true,
                        token,
                        role = "registrar",
                        redirectUrl = "registrar-dashboard.html",
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

               
                await _auditLog.LogAsync("LOGIN_FAILED", $"Failed login attempt (no account found): {email}");
                return Unauthorized(new { success = false, message = "Invalid email or password." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unified login failure.");
                return StatusCode(500, new { success = false, message = "Login processing failed." });
            }
        }

       
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { success = false, message = "Invalid registration data" });
                }

               
                if (!request.DateOfBirth.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Date of birth is required."
                    });
                }

                if (request.DateOfBirth.Value.Date > DateTime.Today)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Date of birth cannot be in the future."
                    });
                }

                int age = DateTime.Today.Year - request.DateOfBirth.Value.Year;

                if (request.DateOfBirth.Value.Date > DateTime.Today.AddYears(-age))
                {
                    age--;
                }

                if (age < 16)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Applicant must be at least 16 years old."
                    });
                }


                string email = request.Email.Trim().ToLower();

                // =====================================================
                // PROGRAMME VALIDATION
                // =====================================================
                bool programmeExists = await _context.Programmes
                    .AnyAsync(p => p.ProgrammeCode == request.ProgrammeCode);

                if (!programmeExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid programme selected."
                    });
                }

                // =====================================================
                // CAMPUS VALIDATION
                // =====================================================
                bool campusExists = await _context.Campuses
                    .AnyAsync(c => c.CampusCode == request.CampusCode);

                if (!campusExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid campus selected."
                    });
                }

                // =====================================================
                // APPLICATION CATEGORY VALIDATION
                // =====================================================
                var validCategories = new[] { "GovernmentLoan", "UniversityBursary", "SelfSponsorship" };
                string applicationCategory = string.IsNullOrWhiteSpace(request.ApplicationCategory)
                    ? "SelfSponsorship"
                    : request.ApplicationCategory.Trim();

                if (!validCategories.Contains(applicationCategory))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid application category. Must be GovernmentLoan, UniversityBursary, or SelfSponsorship."
                    });
                }

                // =====================================================
                // DECLARATION / AGREEMENT VALIDATION
                // =====================================================
                if (!request.AgreementAccepted)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "You must accept the declaration confirming the accuracy of your application before submitting."
                    });
                }

                // =====================================================
                // NATIONAL ID VALIDATION
                // =====================================================
                if (request.IdentificationType == "NATIONAL_ID")
                {
                    if (string.IsNullOrWhiteSpace(request.NationalId))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "National ID is required."
                        });
                    }

                    bool nationalIdExists = await _context.Applications
                        .AnyAsync(a => a.NationalId == request.NationalId);

                    if (nationalIdExists)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "The National ID has already been used."
                        });
                    }
                }

                // =====================================================
                // PASSPORT VALIDATION
                // =====================================================
                if (request.IdentificationType == "PASSPORT")
                {
                    if (string.IsNullOrWhiteSpace(request.PassportNumber))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Passport Number is required."
                        });
                    }

                    bool passportExists = await _context.Applications
                        .AnyAsync(a => a.PassportNumber == request.PassportNumber);

                    if (passportExists)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Passport Number has already been used."
                        });
                    }
                }

                bool exists = await _context.Applications.AnyAsync(a => a.Email == email);
                if (exists)
                {
                    return BadRequest(new { success = false, message = "Email already registered" });
                }

                // AUTOMATIC PROGRAMME CODE MATCHING ENGINE

                string? finalProgrammeName = request.ProgrammeName;

                if (string.IsNullOrWhiteSpace(finalProgrammeName) && !string.IsNullOrWhiteSpace(request.ProgrammeCode))
                {
                    finalProgrammeName = request.ProgrammeCode switch
                    {
                        "BAG" => "Bachelor of Agriculture",
                        "BAH" => "Bachelor of Arts Humanities",
                        "BBA" => "Bachelor of Business Administration",
                        "BCE" => "Bachelor of Civil Engineering",
                        "BED-ENG" => "Bachelor of Education English",
                        "BED-MATH" => "Bachelor of Education Mathematics",
                        "BED-SCI" => "Bachelor of Education Science",
                        "BEE" => "Bachelor of Electrical Engineering",
                        "BES" => "Bachelor of Environmental Science",
                        "BHM" => "Bachelor of Hospitality Management",
                        "BIT" => "Bachelor of Information Technology",
                        "BJC" => "Bachelor of Journalism and Communication",
                        "BME" => "Bachelor of Mechanical Engineering",
                        "BMI" => "Bachelor of Medical Imaging",
                        "BMS" => "Bachelor of Midwifery Science",
                        "BNS" => "Bachelor of Nursing Science",
                        "BPA" => "Bachelor of Public Administration",
                        "BSC" => "Bachelor of Science in Education",
                        "LLB" => "Bachelor of Laws",
                        "MED" => "Master of Education",
                        "MIT" => "Master of Information Technology",
                        "MLS" => "Bachelor of Medical Laboratory Science",
                        "MME" => "Master of Monitoring and Evaluation",
                        "MPA" => "Master of Public Administration",
                        "OPT" => "Bachelor of Optometry",
                        
                        _ => request.ProgrammeCode
                    };
                }

                var application = new Application
                {
                    // ==========================
                    // PERSONAL INFORMATION
                    // ==========================
                    Email = email,
                    FirstName = request.FirstName?.Trim(),
                    MiddleName = request.MiddleName?.Trim(),
                    LastName = request.LastName?.Trim(),
                    DateOfBirth = request.DateOfBirth,
                    Gender = request.Gender,
                    Nationality = request.Nationality?.Trim(),

                    IdentificationType = request.IdentificationType,

                    NationalId = request.NationalId?.Trim(),
                    PassportNumber = request.PassportNumber?.Trim(),
                    PassportPhotoPath = request.PassportPhotoPath?.Trim(),

                    Religion = request.Religion?.Trim(),
                    MaritalStatus = request.MaritalStatus?.Trim(),
                    ApplicationCategory = applicationCategory,
                    LoanSchemeStatus = applicationCategory == "GovernmentLoan" ? "Pending" : null,
                    AgreementAccepted = request.AgreementAccepted,


                    // ==========================
                    // CONTACT INFORMATION
                    // ==========================
                    PrimaryPhone = request.PrimaryPhone?.Trim(),
                    WhatsappPhone = request.WhatsappPhone?.Trim(),
                    District = request.District?.Trim(),
                    PhysicalAddress = request.PhysicalAddress?.Trim(),

                    // ==========================
                    // EMERGENCY CONTACT
                    // ==========================
                    EmergencyContactName = request.EmergencyContactName?.Trim(),
                    EmergencyRelationship = request.EmergencyRelationship?.Trim(),
                    EmergencyPhone = request.EmergencyPhone?.Trim(),

                    // ==========================
                    // ACADEMIC INFORMATION
                    // ==========================
                    ProgrammeCode = request.ProgrammeCode,
                    ProgrammeName = finalProgrammeName,
                    CampusPreference = request.CampusCode,
                    Intake = request.Intake,
                    YearOfEntry = request.YearOfEntry,
                    StudyMode = request.StudyMode,

                    // ==========================
                    // PREVIOUS EDUCATION
                    // ==========================
                    PreviousSchool = request.PreviousSchool?.Trim(),
                    YearOfCompletion = request.YearOfCompletion,
                    CertificateObtained = request.CertificateObtained?.Trim(),
                    SubjectsPassed = request.SubjectsPassed?.Trim(),

                    // ==========================
                    // PAYMENT
                    // ==========================
                    PaymentAmount = request.PaymentAmount.GetValueOrDefault() != 0
         ? (int)request.PaymentAmount!.Value
         : 50000,

                    // ==========================
                    // SECURITY
                    // ==========================
                    PasswordHash = PasswordHasher.HashPassword(request.Password),
                    OtpCode = GenerateSecureOtp(),
                    OtpExpiry = DateTime.UtcNow.AddMinutes(5),

                    // ==========================
                    // SYSTEM FIELDS
                    // ==========================
                    ApplicationDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,

                    Status = "PENDING_REVIEW",
                    ApplicationStatus = "PENDING_REVIEW",
                    PaymentStatus = "PENDING"
                };

              
                string guid = Guid.NewGuid().ToString("N")[..6].ToUpper();
                string dayOfYear = DateTime.UtcNow.DayOfYear.ToString("D3");
                application.ApplicationNumber = $"APP-{DateTime.UtcNow.Year}-{dayOfYear}-{guid}";

              
                if (!string.IsNullOrWhiteSpace(request.PaymentProofPath))
                {
                    application.PaymentProofPath = request.PaymentProofPath.StartsWith('/')
                        ? request.PaymentProofPath
                        : $"/uploads/payment-proofs/{request.PaymentProofPath}";
                }

                _context.Applications.Add(application);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Application created safely for {Email}", email);

                try
                {
                    bool emailSent = await _emailService.SendOtpEmailAsync(
                        application.Email,
                        application.FirstName ?? "Student",
                        application.OtpCode
                    );

                    if (emailSent)
                    {
                        return Ok(new
                        {
                            success = true,
                            message = "Registration successful! OTP sent to your email.",
                            data = new { email = application.Email, applicationId = application.ApplicationId }
                        });
                    }

                    _logger.LogWarning("OTP email failed to send to {Email}", email);
                    return Ok(new
                    {
                        success = true,
                        message = "Registration created but email failed to dispatch. Please contact support.",
                        data = new { email = application.Email, applicationId = application.ApplicationId }
                    });
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Exception sending OTP email to {Email}", email);
                    return Ok(new
                    {
                        success = true,
                        message = "Registration successful. Notification engine error.",
                        data = new { email = application.Email, applicationId = application.ApplicationId }
                    });
                }
            }
            catch (Exception ex)
            {
               
                _logger.LogError(ex, "Registration sequence triggered an error state.");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Registration processing failure.",
                    innerError = ex.Message,
                    sourceTrace = ex.InnerException?.Message
                });
            }
        }


        [HttpPost("upload-passport-photo")]
        public async Task<IActionResult> UploadPassportPhoto(
     int applicationId,
     IFormFile file)
        {
            try
            {
                var application = await _context.Applications
                    .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

                if (application == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Application not found."
                    });
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "No passport photo selected."
                    });
                }

                if (file.Length > 2 * 1024 * 1024)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Passport photo must not exceed 2MB."
                    });
                }

                var extension = Path.GetExtension(file.FileName).ToLower();

                var allowedExtensions = new[]
                {
            ".jpg",
            ".jpeg",
            ".png"
        };

                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Only JPG and PNG images are allowed."
                    });
                }

                string uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads",
                    "passports"
                );

                Directory.CreateDirectory(uploadsFolder);

                string fileName =
                    $"{Guid.NewGuid()}{extension}";

                string fullPath =
                    Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                application.PassportPhotoPath =
                    "/uploads/passports/" + fileName;

                application.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Passport photo uploaded successfully.",
                    path = application.PassportPhotoPath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Passport photo upload failed.");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Passport upload failed."
                });
            }
        }

       
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOTP([FromBody] OtpVerificationRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.OtpCode))
                {
                    return BadRequest(new { success = false, message = "Email and OTP required" });
                }

                string email = request.Email.Trim().ToLower();
                var application = await _context.Applications.FirstOrDefaultAsync(a => a.Email == email);

                if (application == null || application.OtpCode != request.OtpCode)
                {
                    return Unauthorized(new { success = false, message = "Invalid email identity or verification token." });
                }

                if (application.OtpExpiry == null || application.OtpExpiry < DateTime.UtcNow)
                {
                    return Unauthorized(new { success = false, message = "OTP token has expired. Please request a new token code." });
                }

                application.OtpVerifiedAt = DateTime.UtcNow;
                application.OtpCode = null;
                application.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("OTP verified securely for user context: {Email}", email);

                return Ok(new
                {
                    success = true,
                    message = "Email verified successfully! Your application is under active review."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP verification exception state encountered.");
                return StatusCode(500, new { success = false, message = "Verification pipeline failure." });
            }
        }

       
        [HttpPost("student-login")]
        public async Task<IActionResult> StudentLogin([FromBody] StudentLoginRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { success = false, message = "Email and password fields are required" });
                }

                string email = request.Email.Trim().ToLower();

                var student = await _context.Students
     .AsNoTracking()
     .FirstOrDefaultAsync(s => s.Email == email);
                if (student != null)
                {
                    if (!VerifyPassword(request.Password, student.PasswordHash ?? ""))
                    {
                        return Unauthorized(new { success = false, message = "Invalid email or matching password combination." });
                    }

                    string studentToken =
    _jwtService.GenerateJwt(
        student.Email ?? "",
        "Student",
        student.StudentId
    );
                    _logger.LogInformation("Student record successfully authenticated: {Email}", email);
                    return Ok(new
                    {
                        success = true,
                        token = studentToken,
                        user = new
                        {
                            regNumber = student.RegNumber,
                            email = student.Email,
                            firstName = student.FirstName,
                            lastName = student.LastName,
                            programme = student.ProgrammeName,

                           
                            passportPhoto = student.PassportPhotoPath,

                            type = "student"
                        }
                    });
                }

                var application = await _context.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Email == email);
                if (application != null)
                {
                    if (!VerifyPassword(request.Password, application.PasswordHash ?? ""))
                    {
                        return Unauthorized(new { success = false, message = "Invalid email or matching password combination." });
                    }

                    if (application.ApplicationStatus != "APPROVED")
                    {
                        return Unauthorized(new
                        {
                            success = false,
                            message = "Your portal application status is pending review. Check back soon.",
                            status = application.ApplicationStatus
                        });
                    }

                    string token = _jwtService.GenerateJwt(
                     application.Email ?? "",
                     "Applicant",
                     application.ApplicationId
);
                    return Ok(new
                    {
                        success = true,
                        token,
                        user = new { regNumber = application.RegistrationNumber, email = application.Email, firstName = application.FirstName, lastName = application.LastName, type = "applicant" }
                    });
                }

                return Unauthorized(new { success = false, message = "Invalid email or matching password combination." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication state processing failure.");

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    innerException = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Email is required"
                    });
                }

                string email = request.Email.Trim().ToLower();

                string? userRole = null;
                string? firstName = null;


                // ================= STUDENT =================
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email == email);

                if (student != null)
                {
                    userRole = "Student";
                    firstName = student.FirstName;
                }


                // ================= APPLICANT =================
                if (userRole == null)
                {
                    var applicant = await _context.Applications
                        .FirstOrDefaultAsync(a => a.Email == email);

                    if (applicant != null)
                    {
                        userRole = "Applicant";
                        firstName = applicant.FirstName;
                    }
                }


                // ================= LECTURER =================
                if (userRole == null)
                {
                    var lecturer = await _context.Lecturers
                        .FirstOrDefaultAsync(l => l.Email == email);

                    if (lecturer != null)
                    {
                        userRole = "Lecturer";
                        firstName = lecturer.FirstName;
                    }
                }


                // ================= ADMIN =================
                if (userRole == null)
                {
                    var admin = await _context.Admins
                        .FirstOrDefaultAsync(a => a.Email == email);

                    if (admin != null)
                    {
                        userRole = "Admin";
                        firstName = admin.FirstName;
                    }
                }


                // ================= BURSAR =================
                if (userRole == null)
                {
                    var bursar = await _context.Bursars
                        .FirstOrDefaultAsync(b => b.Email == email);

                    if (bursar != null)
                    {
                        userRole = "Bursar";
                        firstName = bursar.FirstName;
                    }
                }


               
                if (userRole == null)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "If the email exists, a password reset link has been sent."
                    });
                }


                string token =
                Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(64)
                );

                var resetRecord = new PasswordResetToken
                {
                    Email = email,
                    UserRole = userRole,
                    ResetToken = token,
                    ExpiryTime = DateTime.UtcNow.AddMinutes(30),
                    Used = false,
                    CreatedAt = DateTime.UtcNow
                };


                _context.PasswordResetTokens.Add(resetRecord);

                await _context.SaveChangesAsync();

                string resetLink = $"https://localhost:44366/reset-password.html?token={token}";


                await _emailService.SendPasswordResetEmailAsync(
                    email,
                    firstName ?? "User",
                    resetLink
                );


                return Ok(new
                {
                    success = true,
                    message = "If the email exists, a password reset link has been sent."
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Forgot password failure");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Password reset processing failed"
                });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
     [FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (request == null ||
                   string.IsNullOrWhiteSpace(request.Token) ||
                   string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Token and password are required"
                    });
                }


                var reset =
                    await _context.PasswordResetTokens
                    .FirstOrDefaultAsync(x =>
                        x.ResetToken == request.Token &&
                        x.Used == false &&
                        x.ExpiryTime > DateTime.UtcNow);


                if (reset == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid or expired reset link"
                    });
                }


                string hashedPassword =
                    PasswordHasher.HashPassword(request.NewPassword);



                switch (reset.UserRole)
                {

                    case "Student":

                        var student =
                            await _context.Students
                            .FirstAsync(s => s.Email == reset.Email);

                        student.PasswordHash = hashedPassword;

                        break;



                    case "Applicant":

                        var applicant =
                            await _context.Applications
                            .FirstAsync(a => a.Email == reset.Email);

                        applicant.PasswordHash = hashedPassword;

                        break;



                    case "Lecturer":

                        var lecturer =
                            await _context.Lecturers
                            .FirstAsync(l => l.Email == reset.Email);

                        lecturer.PasswordHash = hashedPassword;

                        lecturer.MustChangePassword = false;

                        break;



                    case "Admin":

                        var admin =
                            await _context.Admins
                            .FirstAsync(a => a.Email == reset.Email);

                        admin.PasswordHash = hashedPassword;

                        break;



                    case "Bursar":

                        var bursar =
                            await _context.Bursars
                            .FirstAsync(b => b.Email == reset.Email);

                        bursar.PasswordHash = hashedPassword;

                        break;
                }


                reset.Used = true;


                await _context.SaveChangesAsync();


                return Ok(new
                {
                    success = true,
                    message = "Password reset successfully"
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reset password failure");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Password reset failed"
                });
            }
        }

       
        [HttpPost("bursar-login")]
        public async Task<IActionResult> BursarLogin([FromBody] StudentLoginRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { success = false, message = "Email and password fields are required" });
                }

                string email = request.Email.Trim().ToLower();

                var bursar = await _context.Bursars.AsNoTracking().FirstOrDefaultAsync(b => b.Email == email);
                if (bursar == null)
                {
                    return Unauthorized(new { success = false, message = "Invalid email or matching password combination." });
                }

                if (!VerifyPassword(request.Password, bursar.PasswordHash))
                {
                    return Unauthorized(new { success = false, message = "Invalid email or matching password combination." });
                }

                if (bursar.Status != "Active")
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Your bursar account is not active. Contact the system administrator.",
                        status = bursar.Status
                    });
                }

                string token = _jwtService.GenerateJwt(
                    bursar.Email,
                    "Bursar",
                    bursar.BursarId
                );

                _logger.LogInformation("Bursar successfully authenticated: {Email}", email);

                
                await _auditLog.LogAsync(
                    "BURSAR_LOGIN_SUCCESS",
                    $"Successful login for {email}");

                return Ok(new
                {
                    success = true,
                    token,
                    user = new
                    {
                        bursarId = bursar.BursarId,
                        email = bursar.Email,
                        firstName = bursar.FirstName,
                        lastName = bursar.LastName,
                        type = "bursar"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bursar authentication state processing failure.");
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        private static bool VerifyPassword(string inputPassword, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash)) return false;

            if (storedHash.StartsWith("$2"))
            {
                return PasswordHasher.VerifyPassword(inputPassword, storedHash);
            }
            return inputPassword == storedHash;
        }

        private static string GenerateSecureOtp()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }
    }
}