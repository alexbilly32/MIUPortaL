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
    [Route("api/[controller]")]
    public class AdminController(
        MIUContext context,
        AdmissionLetterService admissionLetterService,
        IEmailService emailService,
        ILogger<AdminController> logger,
        IConfiguration configuration)
        : ControllerBase
    {
        private readonly MIUContext _context = context;
        private readonly AdmissionLetterService _admissionLetterService = admissionLetterService;
        private readonly IEmailService _emailService = emailService;
        private readonly ILogger<AdminController> _logger = logger;
        private readonly IConfiguration _configuration = configuration;

        [HttpPost("login")]
        public async Task<IActionResult> AdminLogin(
            [FromBody] AdminLoginRequest? request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Email and password are required"
                    });
                }

                var admin = await _context.Admins
                    .FirstOrDefaultAsync(a => a.Email == request.Email);

                if (admin == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid email or password"
                    });
                }

                bool validPassword = BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    admin.PasswordHash
                );

                if (!validPassword)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid email or password"
                    });
                }

                if (!admin.IsActive)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Admin account is inactive"
                    });
                }

                if (string.IsNullOrWhiteSpace(admin.Email) ||
                    string.IsNullOrWhiteSpace(admin.Role))
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Admin account configuration invalid"
                    });
                }

                
                string token = GenerateJwtToken(
                    admin.AdminId,
                    admin.Email!,
                    admin.Role!
                );

                admin.LastLogin = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Admin login successful: {Email}",
                    admin.Email
                );

                return Ok(new
                {
                    success = true,
                    message = "Login successful",
                    token,
                    admin = new
                    {
                        adminId = admin.AdminId,
                        email = admin.Email,
                        firstName = admin.FirstName,
                        lastName = admin.LastName,
                        role = admin.Role,
                        campus = admin.CampusCode
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin login error");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Login failed",
                    error = ex.Message
                });
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                success = true,
                message = "Admin API running"
            });
        }

        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
               
                int totalApplications = await _context.Applications.CountAsync();
                int pendingApplications = await _context.Applications
                    .CountAsync(a => a.ApplicationStatus == "PENDING_REVIEW");
                int totalStudents = await _context.Students.CountAsync();

                
                int totalLecturers = await _context.Lecturers.CountAsync();
                int activeLecturers = await _context.Lecturers.CountAsync(l => l.Status == "Active");
                int inactiveLecturers = totalLecturers - activeLecturers;

                int totalDeans = await _context.Lecturers.CountAsync(l => l.IsDean);

                int totalFaculties = await _context.Faculties.CountAsync();

                var facultiesWithDeanIds = await _context.Lecturers
                    .Where(l => l.IsDean && l.FacultyId != null)
                    .Select(l => l.FacultyId!.Value)
                    .Distinct()
                    .ToListAsync();

                var facultiesWithoutDeanList = await _context.Faculties
                    .Where(f => !facultiesWithDeanIds.Contains(f.FacultyId))
                    .OrderBy(f => f.FacultyName)
                    .Select(f => new { f.FacultyId, f.FacultyName })
                    .ToListAsync();

                int facultiesWithoutDean = facultiesWithoutDeanList.Count;

                int totalRegistrars = await _context.AcademicRegistrars.CountAsync();
                int totalCourseAssignments = await _context.LecturerCourses.CountAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalApplications,
                        pendingApplications,
                        totalStudents,
                        totalLecturers,
                        activeLecturers,
                        inactiveLecturers,
                        totalDeans,
                        facultiesWithoutDean,
                        facultiesWithoutDeanList,
                        totalRegistrars,
                        totalCourseAssignments
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard statistics");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load dashboard statistics",
                    error = ex.Message
                });
            }
        }

        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications()
        {
            try
            {
                var applications = await _context.Applications
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

                return Ok(new
                {
                    success = true,
                    count = applications.Count,
                    data = applications
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching applications");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Error fetching applications",
                    error = ex.Message
                });
            }
        }

        [HttpGet("students")]
        public async Task<IActionResult> GetStudents()
        {
            try
            {
                var students = await _context.Students
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new
                    {
                        s.StudentId,
                        s.RegNumber,
                        s.FirstName,
                        s.LastName,
                        s.Email,
                        s.ProgrammeCode,
                        s.ProgrammeName,
                        s.CampusCode,
                        s.Status,
                        s.DateAdmitted,
                        s.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    count = students.Count,
                    data = students
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading students",
                    error = ex.Message
                });
            }
        }

        [HttpGet("pending-payments")]
        public async Task<IActionResult> GetPendingPayments()
        {
            try
            {
                var pendingPayments = await _context.Applications
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

                return Ok(new
                {
                    success = true,
                    count = pendingPayments.Count,
                    data = pendingPayments
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading pending payments",
                    error = ex.Message
                });
            }
        }

        [HttpGet("verified-payments")]
        public async Task<IActionResult> GetVerifiedPayments()
        {
            try
            {
                var verifiedPayments = await _context.Applications
                    .Where(a => a.PaymentStatus == "VERIFIED")
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

                return Ok(new
                {
                    success = true,
                    count = verifiedPayments.Count,
                    data = verifiedPayments
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading verified payments",
                    error = ex.Message
                });
            }
        }

        [HttpGet("application/{applicationId}")]
        public async Task<IActionResult> GetApplicationDetails(
            int applicationId)
        {
            try
            {
                var application = await _context.Applications
                    .FirstOrDefaultAsync(a =>
                        a.ApplicationId == applicationId);

                if (application == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Application not found"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = application
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error fetching application details");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Error fetching application details",
                    error = ex.Message
                });
            }
        }

 
        private async Task<string> GenerateRegistrationNumberAsync(
            string programmeCode,
            string campus)
        {
            int year = DateTime.Now.Year % 100;

            string prefix = $"{year}/{programmeCode}/";

            var lastRegNumber = await _context.Students
                .Where(s =>
                    s.RegNumber != null &&
                    s.RegNumber.StartsWith(prefix))
                .OrderByDescending(s => s.RegNumber)
                .Select(s => s.RegNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;

            if (!string.IsNullOrWhiteSpace(lastRegNumber))
            {
                var parts = lastRegNumber.Split('/');

                if (parts.Length >= 3 &&
                    int.TryParse(parts[2], out int lastSequence))
                {
                    nextNumber = lastSequence + 1;
                }
            }

            return
                $"{year}/{programmeCode}/{nextNumber:D3}/{campus}";
        }


        [HttpPost("create-lecturer")]
        public async Task<IActionResult> CreateLecturer(
   [FromBody] CreateLecturerRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Request cannot be empty."
                    });

                if (string.IsNullOrWhiteSpace(request.Email))
                    return BadRequest(new
                    {
                        success = false,
                        message = "Email is required."
                    });

                if (string.IsNullOrWhiteSpace(request.Password))
                    return BadRequest(new
                    {
                        success = false,
                        message = "Password is required."
                    });

                bool exists = await _context.Lecturers
                    .AnyAsync(l => l.Email == request.Email);

                if (exists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "A lecturer with this email already exists."
                    });
                }

                if (request.FacultyId.HasValue)
                {
                    bool facultyExists = await _context.Faculties
                        .AnyAsync(f => f.FacultyId == request.FacultyId);

                    if (!facultyExists)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Faculty does not exist."
                        });
                    }
                }

                var lecturer = new Lecturer
                {
                    Email = request.Email?.Trim() ?? "",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password ?? ""),
                    MustChangePassword = true,
                    FirstName = request.FirstName?.Trim() ?? "",
                    LastName = request.LastName?.Trim() ?? "",
                    PhoneNumber = request.PhoneNumber,
                    FacultyId = request.FacultyId,
                    Status = "Active",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.Lecturers.Add(lecturer);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lecturer account created successfully.",
                    lecturerId = lecturer.LecturerId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating lecturer");

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost("assign-course")]
        public async Task<IActionResult> AssignCourse(
    [FromBody] AssignCourseRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid request."
                    });
                }

                bool lecturerExists = await _context.Lecturers
    .AnyAsync(l =>
        l.LecturerId == request.LecturerId &&
        l.Status == "Active");

                if (!lecturerExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Lecturer not found."
                    });
                }

                bool courseExists = await _context.Courses
                    .AnyAsync(c => c.CourseId == request.CourseId);

                if (!courseExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Course not found."
                    });
                }

                bool alreadyAssigned = await _context.LecturerCourses
                    .AnyAsync(a =>
                        a.CourseId == request.CourseId &&
                        a.AcademicYear == request.AcademicYear &&
                        a.Semester == request.Semester);

                if (alreadyAssigned)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Course is already assigned to another lecturer."
                    });
                }

                var assignment = new LecturerCourse
                {
                    LecturerId = request.LecturerId,
                    CourseId = request.CourseId,
                    AcademicYear = request.AcademicYear,
                    Semester = request.Semester,
                    CreatedAt = DateTime.Now
                };

                _context.LecturerCourses.Add(assignment);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Course assigned successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Assign Course Error");

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // =========================================================
        // FACULTIES
        // =========================================================

        [HttpGet("faculties")]
        public async Task<IActionResult> GetFaculties()
        {
            try
            {
                var faculties = await _context.Faculties
                    .OrderBy(f => f.FacultyName)
                    .Select(f => new
                    {
                        facultyId = f.FacultyId,
                        facultyName = f.FacultyName,
                        facultyCode = f.FacultyCode
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = faculties
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading faculties");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading faculties",
                    error = ex.Message
                });
            }
        }

        [HttpGet("schools-by-faculty/{facultyId}")]
        public async Task<IActionResult> GetSchoolsByFaculty(int facultyId)
        {
            var schools = await _context.Schools
                .Where(s => s.FacultyId == facultyId)
                .OrderBy(s => s.SchoolName)
                .Select(s => new
                {
                    schoolId = s.SchoolId,
                    schoolName = s.SchoolName
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = schools
            });
        }

        [HttpGet("programmes-by-school/{schoolId}")]
        public async Task<IActionResult> GetProgrammesBySchool(int schoolId)
        {
            var programmes = await _context.Programmes
                .Where(p => p.SchoolId == schoolId)
                .OrderBy(p => p.ProgrammeName)
                .Select(p => new
                {
                    programmeCode = p.ProgrammeCode,
                    programmeName = p.ProgrammeName
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = programmes
            });
        }
        [HttpGet("lecturers")]
        public async Task<IActionResult> GetLecturers()
        {
            try
            {
                var lecturers = await _context.Lecturers
                    .Include(l => l.Faculty)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new
                    {
                        l.LecturerId,
                        l.FirstName,
                        l.LastName,
                        l.Email,
                        l.PhoneNumber,
                        l.FacultyId,
                        facultyName = l.Faculty != null ? l.Faculty.FacultyName : null,
                        l.Status
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = lecturers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lecturers");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading lecturers",
                    error = ex.Message
                });
            }
        }



        [HttpGet("programmes-by-faculty/{facultyId}")]
        public async Task<IActionResult> GetProgrammesByFaculty(int facultyId)
        {
            try
            {
                var programmes = await
                    (from p in _context.Programmes
                     join s in _context.Set<School>()
                         on p.SchoolId equals s.SchoolId
                     where s.FacultyId == facultyId
                     orderby p.ProgrammeName
                     select new
                     {
                         p.ProgrammeCode,
                         p.ProgrammeName
                     })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = programmes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("courses-by-programme/{programmeCode}")]
        public async Task<IActionResult> GetCoursesByProgramme(string programmeCode)
        {
            try
            {
                var courses = await _context.Courses
                    .Where(c => c.ProgrammeCode == programmeCode)
                    .OrderBy(c => c.Year)
                    .ThenBy(c => c.Semester)
                    .ThenBy(c => c.CourseCode)
                    .Select(c => new
                    {
                        c.CourseId,
                        c.CourseCode,
                        c.CourseName,
                        c.Year,
                        c.Semester
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = courses
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("lecturers-by-faculty/{facultyId}")]
        public async Task<IActionResult> GetLecturersByFaculty(int facultyId)
        {
            try
            {
                var lecturers = await _context.Lecturers
                    .Where(l =>
                        l.FacultyId == facultyId &&
                        l.Status == "Active")
                    .OrderBy(l => l.FirstName)
                    .ThenBy(l => l.LastName)
                    .Select(l => new
                    {
                        l.LecturerId,
                        FullName = l.FirstName + " " + l.LastName
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = lecturers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }



        [HttpGet("course-assignments")]
        public async Task<IActionResult> GetCourseAssignments()
        {
            try
            {
                var data = await _context.LecturerCourses
                    .Include(a => a.Lecturer)
                    .Include(a => a.Course)
                        .ThenInclude(c => c.Programme)
                            .ThenInclude(p => p.School)
                                .ThenInclude(s => s.Faculty)
                    .OrderByDescending(a => a.CreatedAt)
                   .Select(a => new
                   {
                       assignmentId = a.LecturerCourseId,

                       
                       lecturerId = a.LecturerId,

                       courseId = a.CourseId,

                       facultyId =
        a.Course != null &&
        a.Course.Programme != null &&
        a.Course.Programme.School != null
            ? a.Course.Programme.School.FacultyId
            : (int?)null,

                       schoolId =
        a.Course != null &&
        a.Course.Programme != null
            ? a.Course.Programme.SchoolId
            : (int?)null,

                       programmeCode =
        a.Course != null
            ? a.Course.ProgrammeCode
            : "",

                       
                       lecturerName = a.Lecturer == null
        ? ""
        : a.Lecturer.FirstName + " " + a.Lecturer.LastName,

                       facultyName =
        a.Course != null &&
        a.Course.Programme != null &&
        a.Course.Programme.School != null &&
        a.Course.Programme.School.Faculty != null
            ? a.Course.Programme.School.Faculty.FacultyName
            : "",

                       schoolName =
        a.Course != null &&
        a.Course.Programme != null &&
        a.Course.Programme.School != null
            ? a.Course.Programme.School.SchoolName
            : "",

                       programmeName =
        a.Course != null &&
        a.Course.Programme != null
            ? a.Course.Programme.ProgrammeName
            : "",

                       courseCode =
        a.Course != null
            ? a.Course.CourseCode
            : "",

                       courseName =
        a.Course != null
            ? a.Course.CourseName
            : "",

                       academicYear = a.AcademicYear,

                       semester = a.Semester
                   })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPut("course-assignment/{id}")]
        public async Task<IActionResult> UpdateCourseAssignment(
    int id,
    [FromBody] LecturerCourse model)
        {
            try
            {
                var assignment = await _context.LecturerCourses
                    .FirstOrDefaultAsync(x => x.LecturerCourseId == id);

                if (assignment == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Assignment not found."
                    });
                }

                // Prevent duplicate assignment
                bool duplicate = await _context.LecturerCourses.AnyAsync(x =>
                    x.LecturerCourseId != id &&
                    x.CourseId == model.CourseId &&
                    x.AcademicYear == model.AcademicYear &&
                    x.Semester == model.Semester);

                if (duplicate)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "This course is already assigned."
                    });
                }

                assignment.LecturerId = model.LecturerId;
                assignment.CourseId = model.CourseId;
                assignment.AcademicYear = model.AcademicYear;
                assignment.Semester = model.Semester;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Assignment updated successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpDelete("course-assignment/{id}")]
        public async Task<IActionResult> RemoveCourseAssignment(int id)
        {
            var assignment = await _context.LecturerCourses
                .FindAsync(id);

            if (assignment == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Assignment not found."
                });
            }

            _context.LecturerCourses.Remove(assignment);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Assignment removed."
            });
        }

      

        [HttpDelete("delete-lecturer/{id}")]
        public async Task<IActionResult> DeleteLecturer(int id)
        {
            try
            {
                var lecturer = await _context.Lecturers
                    .FirstOrDefaultAsync(l => l.LecturerId == id);

                if (lecturer == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Lecturer not found"
                    });
                }


                lecturer.Status = "Inactive";
                lecturer.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Lecturer deactivated successfully: {LecturerId}",
                    id
                );

                return Ok(new
                {
                    success = true,
                    message = "Lecturer deactivated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating lecturer");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Error deactivating lecturer",
                    error = ex.Message
                });
            }
        }


        // =========================================================
        // ROLE ASSIGNMENT — DEAN PROMOTION
        // =========================================================

        [HttpPut("promote-to-dean/{lecturerId}")]
        public async Task<IActionResult> PromoteToDean(int lecturerId, [FromBody] PromoteToDeanRequest request)
        {
            try
            {
                var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.LecturerId == lecturerId);

                if (lecturer == null)
                {
                    return NotFound(new { success = false, message = "Lecturer not found" });
                }

                if (lecturer.Status != "Active")
                {
                    return BadRequest(new { success = false, message = "Cannot promote an inactive lecturer" });
                }

                
                if (!lecturer.FacultyId.HasValue && !request.FacultyId.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Lecturer has no Faculty assigned. Provide a FacultyId to assign one as part of this promotion."
                    });
                }

                if (request.FacultyId.HasValue)
                {
                    bool facultyExists = await _context.Faculties.AnyAsync(f => f.FacultyId == request.FacultyId);
                    if (!facultyExists)
                    {
                        return BadRequest(new { success = false, message = "Faculty does not exist" });
                    }
                    lecturer.FacultyId = request.FacultyId;
                }


                var existingDean = await _context.Lecturers
                    .Where(l => l.IsDean && l.FacultyId == lecturer.FacultyId && l.LecturerId != lecturerId)
                    .FirstOrDefaultAsync();

                if (existingDean != null)
                {
                    return Conflict(new
                    {
                        success = false,
                        message = $"{existingDean.FirstName} {existingDean.LastName} is already Dean of this faculty. Revoke that Dean first."
                    });
                }

                lecturer.IsDean = true;
                lecturer.DeanAssignedDate = DateTime.Now;
                lecturer.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Lecturer {LecturerId} promoted to Dean of Faculty {FacultyId}", lecturerId, lecturer.FacultyId);

                return Ok(new
                {
                    success = true,
                    message = $"{lecturer.FirstName} {lecturer.LastName} promoted to Dean successfully. They must log out and log back in for Dean rights to take effect.",
                    facultyId = lecturer.FacultyId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error promoting lecturer to Dean");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("revoke-dean/{lecturerId}")]
        public async Task<IActionResult> RevokeDean(int lecturerId)
        {
            try
            {
                var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.LecturerId == lecturerId);

                if (lecturer == null)
                {
                    return NotFound(new { success = false, message = "Lecturer not found" });
                }

                if (!lecturer.IsDean)
                {
                    return BadRequest(new { success = false, message = "This lecturer is not currently a Dean" });
                }

                lecturer.IsDean = false;
                lecturer.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Dean status revoked for {lecturer.FirstName} {lecturer.LastName}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking Dean status");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("deans")]
        public async Task<IActionResult> GetDeans()
        {
            var deans = await _context.Lecturers
                .Include(l => l.Faculty)
                .Where(l => l.IsDean)
                .Select(l => new
                {
                    l.LecturerId,
                    l.FirstName,
                    l.LastName,
                    l.Email,
                    l.FacultyId,
                    facultyName = l.Faculty != null ? l.Faculty.FacultyName : null,
                    l.DeanAssignedDate,
                    l.Status
                })
                .ToListAsync();

            return Ok(new { success = true, data = deans });
        }

        // =========================================================
        // ACADEMIC REGISTRAR ACCOUNT MANAGEMENT
       // =========================================================

        [HttpPost("create-academic-registrar")]
        public async Task<IActionResult> CreateAcademicRegistrar([FromBody] CreateAcademicRegistrarRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { success = false, message = "Email and password are required." });
                }

                bool exists = await _context.AcademicRegistrars.AnyAsync(r => r.Email == request.Email);
                if (exists)
                {
                    return BadRequest(new { success = false, message = "An Academic Registrar with this email already exists." });
                }

                var registrar = new AcademicRegistrar
                {
                    Email = request.Email.Trim().ToLower(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    FirstName = request.FirstName?.Trim() ?? "",
                    LastName = request.LastName?.Trim() ?? "",
                    PhoneNumber = request.PhoneNumber,
                    Status = "Active",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.AcademicRegistrars.Add(registrar);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Academic Registrar account created successfully.",
                    academicRegistrarId = registrar.AcademicRegistrarId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Academic Registrar");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("academic-registrars")]
        public async Task<IActionResult> GetAcademicRegistrars()
        {
            var registrars = await _context.AcademicRegistrars
                .Select(r => new { r.AcademicRegistrarId, r.FirstName, r.LastName, r.Email, r.PhoneNumber, r.Status })
                .ToListAsync();

            return Ok(new { success = true, data = registrars });
        }
        [HttpPut("toggle-lecturer-status/{id}")]
        public async Task<IActionResult> ToggleLecturerStatus(int id)
        {
            try
            {
                var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.LecturerId == id);

                if (lecturer == null)
                {
                    return NotFound(new { success = false, message = "Lecturer not found" });
                }

                lecturer.Status = lecturer.Status == "Active" ? "Inactive" : "Active";
                lecturer.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Lecturer {(lecturer.Status == "Active" ? "activated" : "deactivated")} successfully",
                    status = lecturer.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling lecturer status");
                return StatusCode(500, new { success = false, message = "Error toggling lecturer status", error = ex.Message });
            }
        }
        private string GenerateJwtToken(
            int adminId,
            string email,
            string role)
        {
            string secretKey =
                _configuration["JwtSettings:SecretKey"]
                ?? throw new Exception(
                    "JWT SecretKey missing");

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    adminId.ToString()),

                new Claim(
                    ClaimTypes.Email,
                    email),

                new Claim(
                    ClaimTypes.Role,
                    role)
            };

            var token = new JwtSecurityToken(
                issuer:
                    _configuration["JwtSettings:Issuer"],

                audience:
                    _configuration["JwtSettings:Audience"],

                claims: claims,

                expires: DateTime.UtcNow.AddHours(24),

                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }


        [HttpPut("lecturers/set-all-status")]
        public async Task<IActionResult> SetAllLecturersStatus([FromQuery] string status)
        {
            try
            {
                if (status != "Active" && status != "Inactive")
                {
                    return BadRequest(new { success = false, message = "Status must be 'Active' or 'Inactive'." });
                }

                var lecturers = await _context.Lecturers.ToListAsync();

                foreach (var lecturer in lecturers)
                {
                    lecturer.Status = status;
                    lecturer.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"{lecturers.Count} lecturer(s) set to {status}.",
                    count = lecturers.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk-updating lecturer status");
                return StatusCode(500, new { success = false, message = "Error updating lecturer statuses", error = ex.Message });
            }
        }
    }


    // =========================================================
    // DTOs 
    // =========================================================

    public class AdminLoginRequest
    {
        public string Email { get; set; } = "";
        public string? Password { get; set; }
    }

    public class ApproveApplicationRequest
    {
        public int ApplicationId { get; set; }
    }

    public class PaymentVerificationRequest
    {
        public int ApplicationId { get; set; }
        public bool IsApproved { get; set; }
        public string? RejectionReason { get; set; }
    }

    public class RejectApplicationRequest
    {
        public int ApplicationId { get; set; }
        public string? RejectionReason { get; set; }
    }

    public class AutoVerifyPaymentRequest
    {
        public string? PaymentReference { get; set; }
        public decimal VerifiedAmount { get; set; }
    }

    public class AssignCourseRequest
    {
        public int LecturerId { get; set; }

        public int CourseId { get; set; }

        public int AcademicYear { get; set; }

        public int Semester { get; set; }
    }
    public class ResultApprovalRequest
    {
        public int ResultId { get; set; }
        public bool IsApproved { get; set; }
    }

    public class PromoteToDeanRequest
    {
        public int? FacultyId { get; set; }
    }

    public class CreateAcademicRegistrarRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
    }
    public class AssignLecturerCourseRequest
    {
        public int LecturerId { get; set; }
        public int CourseId { get; set; }
        public int AcademicYear { get; set; }
        public int Semester { get; set; }
    }
}