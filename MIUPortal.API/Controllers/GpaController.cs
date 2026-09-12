using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Services;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GpaController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly FeeScopingService _feeScopingService;

        public GpaController(MIUContext context, FeeScopingService feeScopingService)
        {
            _context = context;
            _feeScopingService = feeScopingService;
        }

        // ========== GRADE SCALE MAPPING ==========
        private static decimal GetGradePoint(string grade)
        {
            return grade switch
            {
                "A" => 5.0m,
                "B+" => 4.5m,
                "B" => 4.0m,
                "C+" => 3.5m,
                "C" => 3.0m,
                "D+" => 2.5m,
                "D" => 2.0m,
                _ => 0m // F or other
            };
        }

        private static string GetDegreeClass(decimal cgpa)
        {
            return cgpa switch
            {
                >= 4.40m => "🏆 FIRST CLASS HONOURS",
                >= 3.60m => "🥈 SECOND CLASS UPPER DIVISION",
                >= 2.80m => "🥉 SECOND CLASS LOWER DIVISION",
                >= 2.00m => "✅ PASS",
                _ => "❌ FAIL"
            };
        }


        private static int ParseYearNumber(string? currentYearText)
        {
            if (string.IsNullOrWhiteSpace(currentYearText)) return 1;
            var digits = new string(currentYearText.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var y) && y > 0 ? y : 1;
        }


        private static (decimal? mark, string grade, decimal gradePoint, string academicStanding) GetEffectiveResult(Result r)
        {
            string originalGrade = r.Grade ?? "F";
            bool originalFailed = originalGrade == "F";

            if (!originalFailed)
            {
                return (r.Mark, originalGrade, GetGradePoint(originalGrade), "NORMAL PROGRESS");
            }

            if (r.SupplementaryTaken == true && !string.IsNullOrEmpty(r.SupplementaryGrade))
            {
                bool supplementaryPassed = r.SupplementaryGrade != "F";
                decimal supplementaryPoint = r.SupplementaryGradePoint ?? GetGradePoint(r.SupplementaryGrade);
                return (
                    r.SupplementaryMark,
                    r.SupplementaryGrade,
                    supplementaryPoint,
                    supplementaryPassed ? "RETAKEN – CLEARED" : "RETAKEN – STILL FAILED"
                );
            }

            return (r.Mark, originalGrade, 0m, "RETAKE REQUIRED");
        }

        // ========== GET COMPLETE GPA/CGPA TRANSCRIPT ==========
        [HttpGet("{regNumber}")]
        public async Task<IActionResult> GetGPAAndTranscript(string regNumber)
        {
            try
            {
                regNumber = System.Net.WebUtility.UrlDecode(regNumber);

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.RegNumber == regNumber);

                if (student == null)
                    return NotFound(new { message = "Student not found" });

                int currentYear = student.CurrentYear ?? 1;
                int currentSemester = student.CurrentSemester ?? 1;


                var programmeCode = student.ProgrammeCode ?? string.Empty;

                var periodClearances = await _feeScopingService.GetAllPeriodClearanceAsync(student);

                
                var clearanceLookup = new Dictionary<(int, int), MIUPortal.API.Services.PeriodClearance>();
                foreach (var pc in periodClearances)
                {
                    clearanceLookup[(pc.Year, pc.Semester)] = pc;
                }


                var allSignedOffResults = await _context.Results
                    .Where(r => r.RegNumber == regNumber && r.Status == "RegistrarSignedOff")
                    .Include(r => r.Course)
                    .ToListAsync();

                bool WithinRegistrationRange(Result r) =>
                     r.Year < currentYear || (r.Year == currentYear && r.Semester <= currentSemester);

                bool FeeEligible(Result r) =>
                    clearanceLookup.TryGetValue((r.Year ?? 0, r.Semester ?? 0), out var pc) && pc.IsCleared;

                var inRangeResults = allSignedOffResults.Where(WithinRegistrationRange).ToList();
                var visibleResults = inRangeResults.Where(FeeEligible)
                    .OrderBy(r => r.Year).ThenBy(r => r.Semester)
                    .ToList();

                int hiddenDueToFeeClearance = inRangeResults.Count - visibleResults.Count;

                if (visibleResults.Count == 0)
                    return Ok(new
                    {
                        regNumber = student.RegNumber,
                        studentName = $"{student.FirstName} {student.LastName}",
                        currentYear,
                        currentSemester,
                        cgpa = 0m,
                        degreeClass = "❌ NO GRADES YET",
                        currentSemesterGpa = (object?)null,
                        semesterGpas = new List<object>(),
                        transcript = new List<object>(),
                        hiddenDueToFeeClearance,
                        clearance = new
                        {
                            periods = periodClearances
                        }
                    });

                
                var effective = visibleResults.ToDictionary(r => r, r => GetEffectiveResult(r));

                
                var semesterGroups = visibleResults.GroupBy(r => new { r.Year, r.Semester });
                var semesterGpas = new List<object>();

                decimal totalGradePoints = 0;
                int totalCredits = 0;

                foreach (var semGroup in semesterGroups.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Semester))
                {
                    decimal semesterPoints = 0;
                    int semesterCredits = 0;

                    foreach (var result in semGroup)
                    {
                        var (_, _, gradePoint, _) = effective[result];
                        int credits = result.CreditHours ?? (result.Course?.CreditHours ?? 0);

                        semesterPoints += gradePoint * credits;
                        semesterCredits += credits;

                        totalGradePoints += gradePoint * credits;
                        totalCredits += credits;
                    }

                    decimal semesterGpa = semesterCredits == 0 ? 0 : Math.Round(semesterPoints / semesterCredits, 2);
                    bool isCurrent = semGroup.Key.Year == currentYear && semGroup.Key.Semester == currentSemester;

                    semesterGpas.Add(new
                    {
                        year = semGroup.Key.Year,
                        semester = semGroup.Key.Semester,
                        gpa = semesterGpa,
                        courses = semGroup.Count(),
                        creditsEarned = semesterCredits,
                        isCurrent
                    });
                }

               
                decimal cgpa = totalCredits == 0 ? 0 : Math.Round(totalGradePoints / totalCredits, 2);
                string degreeClass = GetDegreeClass(cgpa);

                
                var currentSemResults = visibleResults
                    .Where(r => r.Year == currentYear && r.Semester == currentSemester)
                    .ToList();

                object? currentSemesterGpa = null;
                if (currentSemResults.Count > 0)
                {
                    decimal curPoints = 0;
                    int curCredits = 0;
                    foreach (var r in currentSemResults)
                    {
                        var (_, _, gradePoint, _) = effective[r];
                        int credits = r.CreditHours ?? (r.Course?.CreditHours ?? 0);
                        curPoints += gradePoint * credits;
                        curCredits += credits;
                    }
                    currentSemesterGpa = new
                    {
                        year = currentYear,
                        semester = currentSemester,
                        gpa = curCredits == 0 ? 0 : Math.Round(curPoints / curCredits, 2),
                        courses = currentSemResults.Count,
                        creditsEarned = curCredits
                    };
                }

                // ========== BUILD TRANSCRIPT ==========
                var transcript = visibleResults
                    .Select(r =>
                    {
                        var (effMark, effGrade, effGradePoint, standing) = effective[r];
                        bool isRetake = r.SupplementaryTaken == true;

                        return new
                        {
                            semester = r.Semester,
                            year = r.Year,
                            courseCode = r.CourseCode ?? "",
                            courseName = r.CourseName ?? "",
                            creditHours = r.CreditHours ?? (r.Course?.CreditHours ?? 0),
                            testMark = r.TestMark,
                            testWeight = r.TestWeight,
                            courseworkMark = r.CourseworkMark,
                            courseworkWeight = r.CourseworkWeight,
                            finalExamMark = r.FinalExamMark,
                            finalExamWeight = r.FinalExamWeight,
                            mark = effMark,
                            grade = effGrade,
                            gradePoint = effGradePoint,
                            academicStanding = standing,
                            isRetake,
                            originalMark = isRetake ? r.Mark : null,
                            originalGrade = isRetake ? (r.Grade ?? "F") : null,
                            status = r.Status
                        };
                    })
                    .OrderBy(t => t.year).ThenBy(t => t.semester)
                    .ToList();

                return Ok(new
                {
                    regNumber = student.RegNumber,
                    studentName = $"{student.FirstName} {student.LastName}",
                    programme = student.ProgrammeName,
                    currentYear,
                    currentSemester,
                    cgpa = cgpa,
                    degreeClass = degreeClass,
                    totalCreditsEarned = totalCredits,
                    currentSemesterGpa,
                    semesterGpas = semesterGpas,
                    transcript = transcript,
                    hiddenDueToFeeClearance,
                    clearance = new
                    {
                        periods = periodClearances
                    },
                    gradeScale = new
                    {
                        gradeA = "80+ marks = 5.0 points",
                        gradeBPlus = "75-79 marks = 4.5 points",
                        gradeB = "70-74 marks = 4.0 points",
                        gradeCPlus = "65-69 marks = 3.5 points",
                        gradeC = "60-64 marks = 3.0 points",
                        gradeDPlus = "55-59 marks = 2.5 points",
                        gradeD = "50-54 marks = 2.0 points",
                        gradeF = "Below 50 marks = 0.0 points"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error calculating GPA: {ex.Message}" });
            }
        }
    }
}