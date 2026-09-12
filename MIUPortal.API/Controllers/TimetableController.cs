using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Services;
using System.Security.Claims;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/timetable")]
    [Authorize] 
    public class TimetableController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly ILogger<TimetableController> _logger;
        private readonly AuditLogService _auditLog;

        private const long MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10MB

        public TimetableController(MIUContext context, ILogger<TimetableController> logger, AuditLogService auditLog)
        {
            _context = context;
            _logger = logger;
            _auditLog = auditLog;
        }

        // =====================================================
        // CLAIM HELPERS
        // =====================================================
        private string? GetRole() => User.FindFirst(ClaimTypes.Role)?.Value;

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private bool IsDean() =>
            bool.TryParse(User.FindFirst("isDean")?.Value, out var v) && v;

        private int? GetLecturerFacultyId()
        {
            var claim = User.FindFirst("facultyId")?.Value;
            return int.TryParse(claim, out var id) && id > 0 ? id : null;
        }

       
        private async Task<int?> GetStudentFacultyIdAsync(int studentId)
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
            if (student?.ProgrammeCode == null) return null;

            var programme = await _context.Programmes.FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);
            if (programme?.SchoolId == null) return null;

            var school = await _context.Schools.FirstOrDefaultAsync(s => s.SchoolId == programme.SchoolId);
            return school?.FacultyId;
        }

        // =====================================================
        // SUPPORTING DROPDOWNS (Registrar upload form)
        // =====================================================
        [HttpGet("faculties")]
        public async Task<IActionResult> GetFaculties()
        {
            if (GetRole() != "AcademicRegistrar")
                return Forbid();

            var faculties = await _context.Faculties
                .OrderBy(f => f.FacultyName)
                .Select(f => new { f.FacultyId, f.FacultyName, f.FacultyCode })
                .ToListAsync();

            return Ok(new { success = true, data = faculties });
        }

        [HttpGet("semesters")]
        public async Task<IActionResult> GetSemesters()
        {
            var semesters = await _context.AcademicSemesters
                .OrderByDescending(s => s.StartDate)
                .Select(s => new { s.SemesterId, s.AcademicYear, s.Semester, s.IsActive })
                .ToListAsync();

            return Ok(new { success = true, data = semesters });
        }

        // =====================================================
        // UPLOAD — Registrar (any faculty) or Dean (own faculty only)
        // =====================================================
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] int facultyId, [FromForm] int semesterId)
        {
            try
            {
                var role = GetRole();
                bool isRegistrar = role == "AcademicRegistrar";
                bool isDeanUser = role == "Lecturer" && IsDean();

                if (!isRegistrar && !isDeanUser)
                    return Forbid();

                if (isDeanUser)
                {
                    var deanFacultyId = GetLecturerFacultyId();
                    if (deanFacultyId == null || deanFacultyId != facultyId)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "As Dean, you can only upload the timetable for your own faculty."
                        });
                    }
                }

                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "No file was uploaded." });

                if (file.Length > MAX_FILE_SIZE_BYTES)
                    return BadRequest(new { success = false, message = "File must not exceed 10MB." });

                var extension = Path.GetExtension(file.FileName).ToLower();
                if (extension != ".pdf")
                    return BadRequest(new { success = false, message = "Only PDF files are allowed." });

                var faculty = await _context.Faculties.FirstOrDefaultAsync(f => f.FacultyId == facultyId);
                if (faculty == null)
                    return NotFound(new { success = false, message = "Faculty not found." });

                var semester = await _context.AcademicSemesters.FirstOrDefaultAsync(s => s.SemesterId == semesterId);
                if (semester == null)
                    return NotFound(new { success = false, message = "Academic semester not found." });

              
                var existingActive = await _context.TimetableDocuments
                    .Where(t => t.FacultyId == facultyId && t.SemesterId == semesterId && t.IsActive)
                    .ToListAsync();

                foreach (var old in existingActive)
                    old.IsActive = false;

                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "timetables");
                Directory.CreateDirectory(uploadsFolder);

                string storedFileName = $"{Guid.NewGuid()}{extension}";
                string fullPath = Path.Combine(uploadsFolder, storedFileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                int uploaderId = GetCurrentUserId() ?? 0;
                string uploaderRole = isRegistrar ? "Registrar" : "Dean";
                string uploaderName;

                if (isRegistrar)
                {
                    var registrar = await _context.AcademicRegistrars.FirstOrDefaultAsync(r => r.AcademicRegistrarId == uploaderId);
                    uploaderName = registrar != null ? $"{registrar.FirstName} {registrar.LastName}" : "Academic Registrar";
                }
                else
                {
                    var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.LecturerId == uploaderId);
                    uploaderName = lecturer != null ? $"{lecturer.FirstName} {lecturer.LastName}" : "Dean";
                }

                var document = new TimetableDocument
                {
                    FacultyId = facultyId,
                    SemesterId = semesterId,
                    FileName = file.FileName,
                    FilePath = "/uploads/timetables/" + storedFileName,
                    FileSizeBytes = (int)file.Length,
                    UploadedByRole = uploaderRole,
                    UploadedById = uploaderId,
                    UploadedByName = uploaderName,
                    UploadedAt = DateTime.Now,
                    IsActive = true
                };

                _context.TimetableDocuments.Add(document);
                await _context.SaveChangesAsync();

                await _auditLog.LogAsync(
                    "TIMETABLE_UPLOADED",
                    $"{uploaderRole} {uploaderName} uploaded timetable for {faculty.FacultyName} ({semester.AcademicYear} Semester {semester.Semester})",
                    entityType: "TimetableDocument",
                    entityId: document.TimetableDocumentId);

                return Ok(new
                {
                    success = true,
                    message = "Timetable uploaded successfully",
                    timetableDocumentId = document.TimetableDocumentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Timetable upload failed");
                return StatusCode(500, new { success = false, message = "Timetable upload failed", error = ex.Message });
            }
        }

        // =====================================================
        // CURRENT TIMETABLE — 
        // =====================================================
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent()
        {
            var role = GetRole();
            int? facultyId = null;

            if (role == "Lecturer")
            {
                facultyId = GetLecturerFacultyId();
            }
            else if (role == "Student")
            {
                var studentId = GetCurrentUserId();
                if (studentId.HasValue)
                    facultyId = await GetStudentFacultyIdAsync(studentId.Value);
            }
            else if (role == "AcademicRegistrar")
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Registrars must specify a faculty — use GET /api/timetable/faculty/{facultyId}/current"
                });
            }
            else
            {
                return Forbid();
            }

            if (facultyId == null)
                return NotFound(new { success = false, message = "No faculty could be determined for your account." });

            var doc = await _context.TimetableDocuments
                .Where(t => t.FacultyId == facultyId && t.IsActive)
                .OrderByDescending(t => t.UploadedAt)
                .FirstOrDefaultAsync();

            if (doc == null)
                return NotFound(new { success = false, message = "No timetable has been uploaded yet for your faculty." });

            return Ok(new
            {
                success = true,
                data = new
                {
                    doc.TimetableDocumentId,
                    doc.FacultyId,
                    doc.FileName,
                    doc.FileSizeBytes,
                    doc.UploadedByRole,
                    doc.UploadedByName,
                    doc.UploadedAt,
                    downloadUrl = $"/api/timetable/download/{doc.TimetableDocumentId}"
                }
            });
        }

        
        [HttpGet("faculty/{facultyId}/current")]
        public async Task<IActionResult> GetFacultyCurrent(int facultyId)
        {
            var role = GetRole();
            bool isRegistrar = role == "AcademicRegistrar";
            bool isDeanOfFaculty = role == "Lecturer" && IsDean() && GetLecturerFacultyId() == facultyId;

            if (!isRegistrar && !isDeanOfFaculty)
                return Forbid();

            var doc = await _context.TimetableDocuments
                .Where(t => t.FacultyId == facultyId && t.IsActive)
                .OrderByDescending(t => t.UploadedAt)
                .FirstOrDefaultAsync();

            if (doc == null)
                return NotFound(new { success = false, message = "No active timetable for this faculty." });

            return Ok(new
            {
                success = true,
                data = new
                {
                    doc.TimetableDocumentId,
                    doc.FacultyId,
                    doc.FileName,
                    doc.FileSizeBytes,
                    doc.UploadedByRole,
                    doc.UploadedByName,
                    doc.UploadedAt,
                    downloadUrl = $"/api/timetable/download/{doc.TimetableDocumentId}"
                }
            });
        }

        // =====================================================
        // DOWNLOAD / VIEW — ownership-checked per role
        // =====================================================
        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(int id, [FromQuery] bool download = false)
        {
            var doc = await _context.TimetableDocuments.FirstOrDefaultAsync(t => t.TimetableDocumentId == id);
            if (doc == null)
                return NotFound(new { success = false, message = "Timetable document not found." });

            var role = GetRole();
            bool allowed = false;

            if (role == "AcademicRegistrar")
            {
                allowed = true;
            }
            else if (role == "Lecturer")
            {
                allowed = GetLecturerFacultyId() == doc.FacultyId;
            }
            else if (role == "Student")
            {
                var studentId = GetCurrentUserId();
                if (studentId.HasValue)
                {
                    var studentFacultyId = await GetStudentFacultyIdAsync(studentId.Value);
                    allowed = studentFacultyId == doc.FacultyId;
                }
            }

            if (!allowed)
                return Forbid();

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { success = false, message = "File missing from server storage." });

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);

            
            if (download)
            {
                return File(bytes, "application/pdf", doc.FileName);
            }

            return File(bytes, "application/pdf");
        }

        
        [HttpGet("history/{facultyId}")]
        public async Task<IActionResult> GetHistory(int facultyId)
        {
            var role = GetRole();
            bool isRegistrar = role == "AcademicRegistrar";
            bool isDeanOfFaculty = role == "Lecturer" && IsDean() && GetLecturerFacultyId() == facultyId;

            if (!isRegistrar && !isDeanOfFaculty)
                return Forbid();

            var docs = await _context.TimetableDocuments
                .Where(t => t.FacultyId == facultyId)
                .OrderByDescending(t => t.UploadedAt)
                .Select(t => new
                {
                    t.TimetableDocumentId,
                    t.FileName,
                    t.FileSizeBytes,
                    t.UploadedByRole,
                    t.UploadedByName,
                    t.UploadedAt,
                    t.IsActive,
                    downloadUrl = "/api/timetable/download/" + t.TimetableDocumentId
                })
                .ToListAsync();

            return Ok(new { success = true, count = docs.Count, data = docs });
        }
    }
}