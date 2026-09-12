using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Services;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/grades")]
    [Authorize]
    public class GradeUploadController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly IGradeUploadService _gradeUploadService;
        private readonly IEmailService _emailService;
        private readonly ILogger<GradeUploadController> _logger;

        public GradeUploadController(
      MIUContext context,
      IGradeUploadService gradeUploadService,
      IEmailService emailService,
      ILogger<GradeUploadController> logger)
        {
            _context = context;
            _gradeUploadService = gradeUploadService;
            _emailService = emailService;
            _logger = logger;
        }

        // ========== LECTURER: UPLOAD GRADES (CSV/EXCEL) ==========
        [HttpPost("upload")]
        public async Task<IActionResult> UploadGrades(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "No file provided" });

                // Get lecturer ID from JWT token
                var lecturerIdClaim = User.FindFirst("lecturerId");
                if (lecturerIdClaim == null || !int.TryParse(lecturerIdClaim.Value, out int lecturerId))
                    return Unauthorized(new { success = false, message = "Lecturer not found in token" });

                
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (fileExtension != ".csv" && fileExtension != ".xlsx")
                    return BadRequest(new { success = false, message = "Only CSV and XLSX files are supported" });

                
                using var stream = file.OpenReadStream();
                var result = await _gradeUploadService.ProcessGradesAsync(stream, file.FileName, lecturerId);

                if (!result.Success)
                    return BadRequest(result);

                
                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    fileName = file.FileName,
                    totalRecords = result.Grades?.Count ?? 0,
                    errorCount = result.ErrorCount,
                    errors = result.Errors,
                    grades = result.Grades?.Select(g => new
                    {
                        rowNumber = g.RowNumber,
                        regNumber = g.RegNumber,
                        courseCode = g.CourseCode,
                        courseName = g.CourseName,
                        mark = g.Mark,
                        grade = g.Grade,
                        gradePoint = g.GradePoint,
                        courseworkMark = g.CourseworkMark,
                        testMark = g.TestMark,
                        finalExamMark = g.FinalExamMark,
                        calculatedMark = g.CalculatedMark
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========== ADMIN: APPROVE GRADE BATCH ==========
        [HttpPost("approve-batch")]
        public async Task<IActionResult> ApproveBatch([FromBody] ApproveBatchRequest request)
        {
            try
            {
                if (request?.Grades == null || request.Grades.Count == 0)
                    return BadRequest(new { success = false, message = "No grades provided" });

                var approvedCount = 0;
                var errors = new List<string>();

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    foreach (var gradeRecord in request.Grades)
                    {
                        try
                        {
                            // Find existing result or create new
                            var existingResult = await _context.Results
                                .FirstOrDefaultAsync(r =>
                                    r.RegNumber == gradeRecord.RegNumber &&
                                    r.CourseId == gradeRecord.CourseId);

                            if (existingResult != null)
                            {
                                // Update existing
                                existingResult.Mark = gradeRecord.Mark;
                                existingResult.Grade = gradeRecord.Grade;
                                existingResult.GradePoint = gradeRecord.GradePoint;
                                existingResult.CourseworkMark = gradeRecord.CourseworkMark;
                                existingResult.TestMark = gradeRecord.TestMark;
                                existingResult.FinalExamMark = gradeRecord.FinalExamMark;
                                existingResult.CalculatedMark = gradeRecord.CalculatedMark;
                                existingResult.Status = "Approved";
                                existingResult.ApprovedByAdminId = request.ApprovedByAdminId;
                                existingResult.ApprovalDate = DateTime.Now;
                                existingResult.UpdatedAt = DateTime.Now;

                                _context.Results.Update(existingResult);
                            }
                            else
                            {
                                // Create new result
                                var result = new Result
                                {
                                    RegNumber = gradeRecord.RegNumber,
                                    CourseId = gradeRecord.CourseId,
                                    CourseCode = gradeRecord.CourseCode,
                                    CourseName = gradeRecord.CourseName,
                                    CreditHours = gradeRecord.CreditHours,
                                    Mark = gradeRecord.Mark,
                                    Grade = gradeRecord.Grade,
                                    GradePoint = gradeRecord.GradePoint,
                                    CourseworkMark = gradeRecord.CourseworkMark,
                                    TestMark = gradeRecord.TestMark,
                                    FinalExamMark = gradeRecord.FinalExamMark,
                                    CalculatedMark = gradeRecord.CalculatedMark,
                                    Status = "Approved",
                                    ApprovedByAdminId = request.ApprovedByAdminId,
                                    ApprovalDate = DateTime.Now,
                                    DateUploaded = DateTime.Now,
                                    CreatedAt = DateTime.Now,
                                    UpdatedAt = DateTime.Now
                                };

                                _context.Results.Add(result);
                            }

                            approvedCount++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Student {gradeRecord.RegNumber}, Course {gradeRecord.CourseCode}: {ex.Message}");
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Send emails to students
                    _ = SendGradeNotificationEmailsAsync(request.Grades);

                    return Ok(new
                    {
                        success = true,
                        message = $"Approved {approvedCount} grades successfully",
                        approvedCount = approvedCount,
                        errors = errors
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error approving grade batch");
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========== ADMIN: REJECT GRADE BATCH ==========
        [HttpPost("reject-batch")]
        public async Task<IActionResult> RejectBatch([FromBody] RejectBatchRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Reason))
                    return BadRequest(new { success = false, message = "Rejection reason is required" });

                
                await Task.CompletedTask;

                return Ok(new
                {
                    success = true,
                    message = "Grade batch rejected",
                    reason = request.Reason
                });
            }
            catch (Exception ex)
            {
               
                Console.WriteLine(ex); 
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========== GET STUDENT RESULTS BY REG NUMBER ==========
        [HttpGet("student/{regNumber}")]
        public async Task<IActionResult> GetStudentResults(string regNumber)
        {
            try
            {
                regNumber = System.Net.WebUtility.UrlDecode(regNumber);

                var results = await _context.Results
                    .Where(r => r.RegNumber == regNumber && r.Status == "Approved")
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new
                    {
                        resultId = r.ResultId,
                        courseCode = r.CourseCode,
                        courseName = r.CourseName,
                        creditHours = r.CreditHours,
                        mark = r.Mark,
                        grade = r.Grade,
                        gradePoint = r.GradePoint,
                        courseworkMark = r.CourseworkMark,
                        testMark = r.TestMark,
                        finalExamMark = r.FinalExamMark,
                        approvalDate = r.ApprovalDate
                    })
                    .ToListAsync();

                return Ok(new { success = true, results = results, count = results.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========== SEND GRADE NOTIFICATION EMAILS ==========
        private async Task SendGradeNotificationEmailsAsync(List<GradeRecordRequest> grades)
        {
            try
            {
                var groupedByStudent = grades.GroupBy(g => g.RegNumber);

                foreach (var studentGroup in groupedByStudent)
                {
                    var student = await _context.Students
                        .FirstOrDefaultAsync(s => s.RegNumber == studentGroup.Key);

                    if (student != null && !string.IsNullOrWhiteSpace(student.Email))
                    {
                        var courseList = string.Join(", ", studentGroup.Select(g => $"{g.CourseCode} ({g.Grade})"));
                        var emailBody = $@"
Dear {student.FirstName},

Your grades have been released and approved. Please log in to your portal to view your results:

Courses: {courseList}

Log in to your student portal to see detailed information about your grades, including your GPA and transcript.

Best regards,
MIU Portal
";

                        try
                        {
                            await _emailService.SendApplicationStatusEmailAsync(
    student.Email,
    student.FirstName ?? "",
    "Your Grades Have Been Released"
);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to send email to {student.Email}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending grade notification emails: {ex.Message}");
            }
        }
    }

    // ========== DTOs ==========
    public class ApproveBatchRequest
    {
        public List<GradeRecordRequest>? Grades { get; set; }
        public int ApprovedByAdminId { get; set; }
    }

    public class GradeRecordRequest
    {
        public string? RegNumber { get; set; }
        public int CourseId { get; set; }
        public string? CourseCode { get; set; }
        public string? CourseName { get; set; }
        public int? CreditHours { get; set; }
        public decimal? Mark { get; set; }
        public string? Grade { get; set; }
        public decimal? GradePoint { get; set; }
        public decimal? CourseworkMark { get; set; }
        public decimal? TestMark { get; set; }
        public decimal? FinalExamMark { get; set; }
        public decimal? CalculatedMark { get; set; }
    }

    public class RejectBatchRequest
    {
        public string? Reason { get; set; }
    }
}