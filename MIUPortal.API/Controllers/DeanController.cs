using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Services;

namespace MIUPortal.API.Controllers
{
   
    [ApiController]
    [Route("api/dean")]
    [Authorize(Roles = "Lecturer")]
    public class DeanController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly ILogger<DeanController> _logger;
        private readonly AuditLogService _auditLog;

        public DeanController(MIUContext context, ILogger<DeanController> logger, AuditLogService auditLog)
        {
            _context = context;
            _logger = logger;
            _auditLog = auditLog;
        }

        
        private (int lecturerId, int? facultyId, bool isDean) GetDeanContext()
        {
            int lecturerId = int.Parse(User.FindFirst("lecturerId")?.Value ?? "0");

            var facultyClaim = User.FindFirst("facultyId")?.Value;
            int? facultyId = (!string.IsNullOrEmpty(facultyClaim) && facultyClaim != "0")
                ? int.Parse(facultyClaim)
                : null;

            bool isDean = bool.TryParse(User.FindFirst("isDean")?.Value, out var d) && d;

            return (lecturerId, facultyId, isDean);
        }

       
        private IActionResult? RequireDean(out int lecturerId, out int facultyId)
        {
            var (lid, fid, isDean) = GetDeanContext();
            lecturerId = lid;
            facultyId = fid ?? 0;

            if (!isDean)
            {
                return new ObjectResult(new { success = false, message = "Dean privileges required for this action" })
                { StatusCode = 403 };
            }

            if (fid == null)
            {
                return new ObjectResult(new { success = false, message = "Dean account has no Faculty assigned. Contact Admin." })
                { StatusCode = 400 };
            }

            return null;
        }

        // ===============================
        // DASHBOARD STATS 
        // ===============================
        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var blocked = RequireDean(out _, out int facultyId);
            if (blocked != null) return blocked;

            try
            {
                var facultyCourseIds = await
                    (from c in _context.Courses
                     join p in _context.Programmes on c.ProgrammeCode equals p.ProgrammeCode into pj
                     from p in pj.DefaultIfEmpty()
                     join s in _context.Schools on p.SchoolId equals s.SchoolId into sj
                     from s in sj.DefaultIfEmpty()
                     where s != null && s.FacultyId == facultyId
                     select c.CourseId)
                    .ToListAsync();

                int pendingCount = await _context.Results
                    .CountAsync(r => r.Status == "Pending" && facultyCourseIds.Contains(r.CourseId));

                int deanApprovedCount = await _context.Results
                    .CountAsync(r => r.Status == "DeanApproved" && facultyCourseIds.Contains(r.CourseId));

                int signedOffCount = await _context.Results
                    .CountAsync(r => r.Status == "RegistrarSignedOff" && facultyCourseIds.Contains(r.CourseId));

                int rejectedCount = await _context.Results
                    .CountAsync(r => r.Status == "Rejected" && facultyCourseIds.Contains(r.CourseId) && r.ApprovedByDeanId != null);

                return Ok(new
                {
                    success = true,
                    data = new { pendingCount, deanApprovedCount, signedOffCount, rejectedCount }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Dean dashboard stats");
                return StatusCode(500, new { success = false, message = "Error loading dashboard stats", error = ex.Message });
            }
        }

       
        [HttpGet("results/pending")]
        public async Task<IActionResult> GetPendingResults()
        {
            var blocked = RequireDean(out _, out int facultyId);
            if (blocked != null) return blocked;

            try
            {
                var facultyCourseIds = await
                    (from c in _context.Courses
                     join p in _context.Programmes on c.ProgrammeCode equals p.ProgrammeCode into pj
                     from p in pj.DefaultIfEmpty()
                     join s in _context.Schools on p.SchoolId equals s.SchoolId into sj
                     from s in sj.DefaultIfEmpty()
                     where s != null && s.FacultyId == facultyId
                     select c.CourseId)
                    .ToListAsync();

                var results = await _context.Results
                    .Where(r => r.Status == "Pending" && facultyCourseIds.Contains(r.CourseId))
                    .Include(r => r.Student)
                    .OrderByDescending(r => r.DateUploaded)
                    .Select(r => new
                    {
                        r.ResultId,
                        regNumber = r.RegNumber,
                        studentName = r.Student != null ? $"{r.Student.FirstName} {r.Student.LastName}" : "N/A",
                        courseCode = r.CourseCode,
                        courseName = r.CourseName,
                        mark = r.Mark,
                        grade = r.Grade,
                        uploadedByLecturerId = r.UploadedByLecturerId,
                        dateUploaded = r.DateUploaded
                    })
                    .ToListAsync();

                return Ok(new { success = true, count = results.Count, data = results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending results for Dean");
                return StatusCode(500, new { success = false, message = "Error loading pending results", error = ex.Message });
            }
        }

       
       
        [HttpPost("results/approve")]
        public async Task<IActionResult> ApproveResult([FromBody] ResultApprovalRequest request)
        {
            var blocked = RequireDean(out int lecturerId, out int facultyId);
            if (blocked != null) return blocked;

            try
            {
                if (request == null || request.ResultId <= 0)
                {
                    return BadRequest(new { success = false, message = "ResultId is required" });
                }

                var result = await _context.Results.FirstOrDefaultAsync(r => r.ResultId == request.ResultId);

                if (result == null)
                {
                    return NotFound(new { success = false, message = "Result not found" });
                }

                if (result.Status != "Pending")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Result status is '{result.Status}'. Only Pending results can be approved by a Dean."
                    });
                }

               
                bool inMyFaculty = await
                    (from c in _context.Courses
                     join p in _context.Programmes on c.ProgrammeCode equals p.ProgrammeCode into pj
                     from p in pj.DefaultIfEmpty()
                     join s in _context.Schools on p.SchoolId equals s.SchoolId into sj
                     from s in sj.DefaultIfEmpty()
                     where c.CourseId == result.CourseId && s != null && s.FacultyId == facultyId
                     select c.CourseId)
                    .AnyAsync();

                if (!inMyFaculty)
                {
                    return StatusCode(403, new { success = false, message = "This result belongs to a course outside your faculty" });
                }

                result.Status = request.IsApproved ? "DeanApproved" : "Rejected";
                result.ApprovedByDeanId = lecturerId;
                result.DeanApprovalDate = DateTime.Now;
                result.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                await _auditLog.LogAsync(
                    request.IsApproved ? "RESULT_DEAN_APPROVED" : "RESULT_DEAN_REJECTED",
                    $"{(request.IsApproved ? "Approved" : "Rejected")} result #{result.ResultId} ({result.CourseCode}) for {result.RegNumber}",
                    deanLecturerId: lecturerId,
                    regNumber: result.RegNumber,
                    entityType: "Result",
                    entityId: result.ResultId);

                return Ok(new
                {
                    success = true,
                    message = request.IsApproved
                        ? "Result approved and forwarded to Academic Registrar for sign-off"
                        : "Result rejected"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving result as Dean");
                return StatusCode(500, new { success = false, message = "Error approving result", error = ex.Message });
            }
        }
    }
}