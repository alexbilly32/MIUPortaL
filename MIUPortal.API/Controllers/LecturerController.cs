using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using ClosedXML.Excel;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/lecturer")]
    [Authorize(Roles = "Lecturer")]
    public class LecturerController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly ILogger<LecturerController> _logger;

        public LecturerController(MIUContext context, ILogger<LecturerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Helper: pull the logged-in lecturer's id + faculty straight from the JWT
        private (int lecturerId, int? facultyId) GetLecturerContext()
        {
            int lecturerId = int.Parse(User.FindFirst("lecturerId")?.Value ?? "0");
            var facultyClaim = User.FindFirst("facultyId")?.Value;
            int? facultyId = (!string.IsNullOrEmpty(facultyClaim) && facultyClaim != "0")
                ? int.Parse(facultyClaim)
                : null;
            return (lecturerId, facultyId);
        }

        // ========== DASHBOARD STATS ==========
        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var (lecturerId, facultyId) = GetLecturerContext();

                var myCourseIds = await _context.LecturerCourses
                    .Where(lc => lc.LecturerId == lecturerId)
                    .Select(lc => lc.CourseId)
                    .ToListAsync();

                int assignedCoursesCount = myCourseIds.Count;

                int totalStudentsCount = await _context.CourseEnrollments
                    .Where(e => myCourseIds.Contains(e.CourseId))
                    .Select(e => e.RegNumber)
                    .Distinct()
                    .CountAsync();

                var myCourseCodes = await _context.Courses
                    .Where(c => myCourseIds.Contains(c.CourseId))
                    .Select(c => c.CourseCode)
                    .ToListAsync();

                int pendingGradesCount = await _context.Results
                    .Where(r => myCourseIds.Contains(r.CourseId) && r.Status == "Pending")
                    .CountAsync();

                int approvedGradesCount = await _context.Results
                    .Where(r => myCourseIds.Contains(r.CourseId) && r.Status == "Approved")
                    .CountAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        assignedCoursesCount,
                        totalStudentsCount,
                        pendingGradesCount,
                        approvedGradesCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lecturer dashboard stats");
                return StatusCode(500, new { success = false, message = "Error loading dashboard stats", error = ex.Message });
            }
        }

       
        [HttpGet("courses")]
        public async Task<IActionResult> GetCourses()
        {
            try
            {
                var (lecturerId, facultyId) = GetLecturerContext();

                var myCourseIds = await _context.LecturerCourses
                    .Where(lc => lc.LecturerId == lecturerId)
                    .Select(lc => lc.CourseId)
                    .ToListAsync();

                
                var courseFacultyMap = await
                    (from c in _context.Courses
                     join p in _context.Programmes on c.ProgrammeCode equals p.ProgrammeCode into pj
                     from p in pj.DefaultIfEmpty()
                     join s in _context.Schools on p.SchoolId equals s.SchoolId into sj
                     from s in sj.DefaultIfEmpty()
                     select new { c.CourseId, DerivedFacultyId = s != null ? (int?)s.FacultyId : null })
                    .ToListAsync();

                var relevantCourseIds = courseFacultyMap
                    .Where(x => myCourseIds.Contains(x.CourseId) ||
                                (facultyId != null && x.DerivedFacultyId == facultyId))
                    .Select(x => x.CourseId)
                    .Distinct()
                    .ToList();

                if (relevantCourseIds.Count == 0)
                {
                    return Ok(new { success = true, count = 0, data = new List<object>(), message = "No courses found for your faculty or assignments." });
                }

                var courses = await _context.Courses
                    .Where(c => relevantCourseIds.Contains(c.CourseId) && (c.IsActive == null || c.IsActive == true))
                    .OrderBy(c => c.CourseCode)
                    .ToListAsync();

                var courseIds = courses.Select(c => c.CourseId).ToList();

                var studentCounts = await _context.CourseEnrollments
                    .Where(e => courseIds.Contains(e.CourseId))
                    .GroupBy(e => e.CourseId)
                    .Select(g => new { CourseId = g.Key, Count = g.Select(e => e.RegNumber).Distinct().Count() })
                    .ToListAsync();

                var result = courses.Select(c => new
                {
                    c.CourseId,
                    c.CourseCode,
                    c.CourseName,
                    c.Year,
                    c.Semester,
                    c.CreditHours,
                    studentCount = studentCounts.FirstOrDefault(sc => sc.CourseId == c.CourseId)?.Count ?? 0,
                    isAssignedToMe = myCourseIds.Contains(c.CourseId),
                    isActive = c.IsActive ?? true
                });

                return Ok(new { success = true, count = courses.Count, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lecturer courses");
                return StatusCode(500, new { success = false, message = "Error loading courses", error = ex.Message });
            }
        }

       
        [HttpGet("students")]
        public async Task<IActionResult> GetStudents()
        {
            try
            {
                var (lecturerId, facultyId) = GetLecturerContext();

                var myCourseIds = await _context.LecturerCourses
                    .Where(lc => lc.LecturerId == lecturerId)
                    .Select(lc => lc.CourseId)
                    .ToListAsync();

                var courseFacultyMap = await
                    (from c in _context.Courses
                     join p in _context.Programmes on c.ProgrammeCode equals p.ProgrammeCode into pj
                     from p in pj.DefaultIfEmpty()
                     join s in _context.Schools on p.SchoolId equals s.SchoolId into sj
                     from s in sj.DefaultIfEmpty()
                     select new { c.CourseId, DerivedFacultyId = s != null ? (int?)s.FacultyId : null })
                    .ToListAsync();

                var relevantCourseIds = courseFacultyMap
                    .Where(x => myCourseIds.Contains(x.CourseId) ||
                                (facultyId != null && x.DerivedFacultyId == facultyId))
                    .Select(x => x.CourseId)
                    .Distinct()
                    .ToList();

                if (relevantCourseIds.Count == 0)
                {
                    return Ok(new { success = true, count = 0, data = new List<object>(), message = "No student belongs to this faculty." });
                }

                var enrollments = await _context.CourseEnrollments
                    .Where(e => relevantCourseIds.Contains(e.CourseId))
                    .Include(e => e.Student)
                    .Include(e => e.Course)
                    .ToListAsync();

                var result = enrollments
                    .Where(e => e.Student != null)
                    .Select(e => new
                    {
                        regNumber = e.RegNumber,
                        name = $"{e.Student!.FirstName} {e.Student.LastName}",
                        email = e.Student.Email,
                        courseCode = e.Course?.CourseCode,
                        courseName = e.Course?.CourseName,
                        status = e.Status ?? e.Student.Status ?? "ACTIVE"
                    })
                    .ToList();

                if (result.Count == 0)
                {
                    return Ok(new { success = true, count = 0, data = new List<object>(), message = "No student belongs to this faculty." });
                }

                return Ok(new { success = true, count = result.Count, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lecturer students");
                return StatusCode(500, new { success = false, message = "Error loading students", error = ex.Message });
            }
        }

        
        [HttpGet("grades")]
        public async Task<IActionResult> GetGrades()
        {
            try
            {
                var (lecturerId, facultyId) = GetLecturerContext();

                var myCourseIds = await _context.LecturerCourses
                    .Where(lc => lc.LecturerId == lecturerId)
                    .Select(lc => lc.CourseId)
                    .ToListAsync();

                var results = await _context.Results
                    .Where(r => myCourseIds.Contains(r.CourseId))
                    .Include(r => r.Student)
                    .OrderByDescending(r => r.DateUploaded)
                    .ToListAsync();

                var data = results.Select(r => new
                {
                    r.ResultId,
                    regNumber = r.RegNumber,
                    name = r.Student != null ? $"{r.Student.FirstName} {r.Student.LastName}" : "N/A",
                    courseCode = r.CourseCode,
                    courseName = r.CourseName,
                    mark = r.Mark,
                    grade = r.Grade,
                    status = r.Status,
                    dateUploaded = r.DateUploaded
                });

                return Ok(new { success = true, count = results.Count, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading grade management data");
                return StatusCode(500, new { success = false, message = "Error loading grades", error = ex.Message });
            }
        }

      
        [HttpGet("results")]
        public async Task<IActionResult> GetResults([FromQuery] int? courseId)
        {
            try
            {
                var (lecturerId, facultyId) = GetLecturerContext();

                var myCourseIds = await _context.LecturerCourses
                    .Where(lc => lc.LecturerId == lecturerId)
                    .Select(lc => lc.CourseId)
                    .ToListAsync();

                var query = _context.Results
                    .Where(r => myCourseIds.Contains(r.CourseId) && r.Status == "Approved");

                if (courseId.HasValue)
                {
                    query = query.Where(r => r.CourseId == courseId.Value);
                }

                var results = await query
                    .Include(r => r.Student)
                    .OrderBy(r => r.CourseCode)
                    .ThenBy(r => r.RegNumber)
                    .ToListAsync();

                var data = results.Select(r => new
                {
                    regNumber = r.RegNumber,
                    name = r.Student != null ? $"{r.Student.FirstName} {r.Student.LastName}" : "N/A",
                    courseCode = r.CourseCode,
                    courseName = r.CourseName,
                    mark = r.Mark,
                    grade = r.Grade,
                    gradePoint = r.GradePoint
                });

                return Ok(new { success = true, count = results.Count, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading results");
                return StatusCode(500, new { success = false, message = "Error loading results", error = ex.Message });
            }
        }

        // ========== UPLOAD GRADES (CSV) ==========
       
        [HttpPost("grades/upload")]
        public async Task<IActionResult> UploadGrades(IFormFile file)
        {
            try
            {
                var (lecturerId, facultyId) = GetLecturerContext();

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "No file uploaded" });
                }

                string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                List<string[]> rows;

                if (extension == ".csv")
                {
                    rows = await ParseCsvRows(file);
                }
                else if (extension == ".xlsx" || extension == ".xls")
                {
                    rows = ParseExcelRows(file);
                }
                else
                {
                    return BadRequest(new { success = false, message = "Unsupported file type. Please upload a .csv or .xlsx file." });
                }

                var myCourseCodes = await _context.LecturerCourses
            .Where(lc => lc.LecturerId == lecturerId)
            .Join(_context.Courses, lc => lc.CourseId, c => c.CourseId, (lc, c) => new { c.CourseId, c.CourseCode, c.CourseName, c.Semester, c.Year })
            .ToListAsync();

                var uploaded = new List<object>();
                var errors = new List<string>();

                
                for (int i = 1; i < rows.Count; i++)
                {
                    int lineNum = i + 1; 
                    var parts = rows[i];

                    if (parts.Length < 5)
                    {
                        errors.Add($"Line {lineNum}: expected 5 columns, got {parts.Length}");
                        continue;
                    }

                    static string CleanField(string raw) => (raw ?? "").Trim().Trim('"').Trim();

                    string regNumber = CleanField(parts[0]);
                    string courseCode = CleanField(parts[1]);

                    if (string.IsNullOrWhiteSpace(regNumber) || string.IsNullOrWhiteSpace(courseCode))
                    {
                        continue; 
                    }

                    var courseMatch = myCourseCodes.FirstOrDefault(c => c.CourseCode == courseCode);
                    if (courseMatch == null)
                    {
                        errors.Add($"Line {lineNum}: course '{courseCode}' is not assigned to you");
                        continue;
                    }

                    var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
                    if (student == null)
                    {
                        errors.Add($"Line {lineNum}: student '{regNumber}' not found");
                        continue;
                    }

                    if (!decimal.TryParse(CleanField(parts[2]), out decimal courseworkMark) ||
                        !decimal.TryParse(CleanField(parts[3]), out decimal testMark) ||
                        !decimal.TryParse(CleanField(parts[4]), out decimal finalExamMark))
                    {
                        errors.Add($"Line {lineNum}: invalid mark values");
                        continue;
                    }

                    decimal finalMark = Math.Round((courseworkMark * 0.20m)+ (testMark * 0.20m) + (finalExamMark * 0.60m), 2);
                    var (grade, gradePoint) = CalculateGrade(finalMark);

                    var existing = await _context.Results
                        .FirstOrDefaultAsync(r => r.RegNumber == regNumber && r.CourseId == courseMatch.CourseId);

                    if (existing != null)
                    {
                        existing.CourseworkMark = courseworkMark;
                        existing.TestMark = testMark;
                        existing.FinalExamMark = finalExamMark;
                        existing.CalculatedMark = finalMark;
                        existing.Mark = finalMark;
                        existing.Grade = grade;
                        existing.GradePoint = gradePoint;
                        existing.CourseName = courseMatch.CourseName;
                        existing.Semester = courseMatch.Semester;
                        existing.Year = courseMatch.Year;
                        existing.Status = "Pending";
                        existing.UploadedByLecturerId = lecturerId;
                        existing.DateUploaded = DateTime.Now;
                        existing.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        _context.Results.Add(new Result
                        {
                            RegNumber = regNumber,
                            CourseId = courseMatch.CourseId,
                            CourseCode = courseCode,
                            CourseName = courseMatch.CourseName,
                            Semester = courseMatch.Semester,
                            Year = courseMatch.Year,
                            CourseworkMark = courseworkMark,
                            TestMark = testMark,
                            FinalExamMark = finalExamMark,
                            CalculatedMark = finalMark,
                            Mark = finalMark,
                            Grade = grade,
                            GradePoint = gradePoint,
                            Status = "Pending",
                            UploadedByLecturerId = lecturerId,
                            DateUploaded = DateTime.Now,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        });
                    }

                    uploaded.Add(new { regNumber, courseCode, mark = finalMark, grade });
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"{uploaded.Count} grade(s) uploaded and pending admin approval",
                    uploadedCount = uploaded.Count,
                    errorCount = errors.Count,
                    data = uploaded,
                    errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading grades");
                return StatusCode(500, new { success = false, message = "Error uploading grades", error = ex.Message });
            }
        }

        // ========== CSV ROW PARSER ==========
        private static async Task<List<string[]>> ParseCsvRows(IFormFile file)
        {
            var rows = new List<string[]>();
            using var reader = new StreamReader(file.OpenReadStream());

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                rows.Add(line.Split(','));
            }

            return rows;
        }

        // ========== EXCEL ROW PARSER (requires ClosedXML) ==========
        private static List<string[]> ParseExcelRows(IFormFile file)
        {
            var rows = new List<string[]>();

            using var stream = file.OpenReadStream();
            using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1); // first sheet

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null) return rows;

            foreach (var row in usedRange.RowsUsed())
            {
                var cells = row.Cells(1, 5); 
                var values = cells.Select(c => c.GetString()).ToArray();
                rows.Add(values);
            }

            return rows;
        }
        // ========== GRADE CALCULATION HELPER ==========
        private static (string grade, decimal gradePoint) CalculateGrade(decimal mark)
        {
            if (mark >= 80) return ("A", 5.0m);
            if (mark >= 75) return ("B+", 4.5m);
            if (mark >= 70) return ("B", 4.0m);
            if (mark >= 65) return ("C+", 3.5m);
            if (mark >= 60) return ("C", 3.0m);
            if (mark >= 55) return ("D+", 2.5m);
            if (mark >= 50) return ("D", 2.0m);
            return ("F", 0.0m);
        }
    }
}