using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResultsController : ControllerBase
    {
        private readonly MIUContext _context;

        public ResultsController(MIUContext context)
        {
            _context = context;
        }

        // =========================
        // GRADE LOGIC (STATIC)
        // =========================
        private static string GetGrade(decimal mark)
        {
            return mark switch
            {
                >= 80 => "A",
                >= 75 => "B+",
                >= 70 => "B",
                >= 65 => "C+",
                >= 60 => "C",
                >= 55 => "D+",
                >= 50 => "D",
                _ => "F"
            };
        }

        private static decimal GetGradePoint(decimal mark)
        {
            return mark switch
            {
                >= 80 => 5.0m,
                >= 75 => 4.5m,
                >= 70 => 4.0m,
                >= 65 => 3.5m,
                >= 60 => 3.0m,
                >= 55 => 2.5m,
                >= 50 => 2.0m,
                _ => 0m
            };
        }

        private static bool IsEligibleForSupplementary(decimal mark)
            => mark >= 40 && mark < 50;

        private static string GetDegreeClass(decimal cgpa)
        {
            return cgpa switch
            {
                >= 4.40m => "FIRST CLASS",
                >= 3.60m => "SECOND UPPER",
                >= 2.80m => "SECOND LOWER",
                >= 2.00m => "PASS",
                _ => "FAIL"
            };
        }

        private static decimal GetEffectiveGradePoint(Result r)
        {
            if (r.SupplementaryTaken && r.SupplementaryGradePoint.HasValue)
                return r.SupplementaryGradePoint.Value;

            return r.GradePoint ?? 0m;
        }

        // =========================
        // UPLOAD RESULT
        // =========================
        [HttpPost("upload")]
        public async Task<IActionResult> UploadResult([FromBody] UploadResultRequest request)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.RegNumber == request.RegNumber);

            if (student == null)
                return NotFound("Student not found");

            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseId == request.CourseId);

            if (course == null)
                return NotFound("Course not found");

            var result = new Result
            {
                RegNumber = request.RegNumber,
                CourseId = request.CourseId,
                SemesterId = request.SemesterId,
                Mark = request.Mark,
                Grade = GetGrade(request.Mark),
                GradePoint = GetGradePoint(request.Mark),
                IsSupplementaryEligible = IsEligibleForSupplementary(request.Mark),
                Status = "Pending",
                DateUploaded = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            _context.Results.Add(result);
            await _context.SaveChangesAsync();



            return Ok(new { message = "Result uploaded successfully" });
        }

        // =========================
        // GET STUDENT RESULTS
        // =========================
        [HttpGet("student/{regNumber}")]
        public async Task<IActionResult> GetStudentResults(string regNumber)
        {
            var results = await GetLatestResults(regNumber);

            var response = results
                .OrderBy(r => r.SemesterId)
                .Select(r => new
                {
                    r.ResultId,
                    r.SemesterId,
                    CourseCode = r.Course?.CourseCode ?? "",
                    CourseName = r.Course?.CourseName ?? "",
                    CreditHours = r.Course?.CreditHours ?? 0,
                    r.Mark,
                    r.Grade,
                    r.GradePoint,
                    r.Status
                });

            return Ok(response);
        }

        // =========================
        // CGPA
        // =========================
        [HttpGet("cgpa/{regNumber}")]
        public async Task<IActionResult> GetCGPA(string regNumber)
        {
            var results = await _context.Results
                .Include(r => r.Course)
                .Where(r => r.RegNumber == regNumber && r.Status == "Approved")
                .ToListAsync();

            if (results.Count == 0)
                return NotFound("No approved results found");

            decimal totalPoints = 0;
            int totalCredits = 0;

            foreach (var r in results)
            {
                if (r.Course == null) continue;

                totalPoints += GetEffectiveGradePoint(r) * r.Course.CreditHours;
                totalCredits += r.Course.CreditHours;
            }

            var cgpa = totalCredits == 0 ? 0 : Math.Round(totalPoints / totalCredits, 2);

            return Ok(new
            {
                RegNumber = regNumber,
                CGPA = cgpa,
                DegreeClass = GetDegreeClass(cgpa)
            });
        }

        // =========================
        // HELPERS
        // =========================
        private async Task<List<Result>> GetLatestResults(string regNumber)
        {
            var latestSemester = await _context.Results
                .Where(r => r.RegNumber == regNumber && r.Status == "Approved")
                .MaxAsync(r => (int?)r.SemesterId);

            if (latestSemester == null)
                return [];

            return await _context.Results
                .Include(r => r.Course)
                .Where(r => r.RegNumber == regNumber &&
                            r.Status == "Approved" &&
                            r.SemesterId == latestSemester)
                .ToListAsync();
        }

        // =========================
        // DTOs
        // =========================
    }

    public class UploadResultRequest
    {
        public string RegNumber { get; set; } = "";
        public int CourseId { get; set; }
        public int SemesterId { get; set; }
        public decimal Mark { get; set; }
    }

    public class BulkUploadRequest
    {
        public List<UploadResultRequest> Results { get; set; } = new();
    }
}