using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Services
{
    public interface IGradeUploadService
    {
        Task<GradeUploadResult> ProcessGradesAsync(Stream fileStream, string fileName, int lecturerId);
    }

    public class GradeUploadService(MIUContext context) : IGradeUploadService
    {
        private readonly MIUContext _context = context;

        // ========== GRADE SCALE MAPPING ==========
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
                _ => 0m
            };
        }

        // ========== PROCESS GRADES FROM FILE ==========
        public async Task<GradeUploadResult> ProcessGradesAsync(Stream fileStream, string fileName, int lecturerId)
        {
            var result = new GradeUploadResult();

            try
            {
                // Verify lecturer exists
                var lecturer = await _context.Lecturers
                    .Include(l => l.LecturerCourses)
                    .FirstOrDefaultAsync(l => l.LecturerId == lecturerId);

                if (lecturer == null)
                {
                    result.Success = false;
                    result.Message = "Lecturer not found";
                    return result;
                }

                // Parse file
                var grades = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                    ? ParseCsvFile(fileStream)
                    : ParseExcelFile();

                if (grades.Count == 0)
                {
                    result.Success = false;
                    result.Message = "No valid grade records found in file";
                    return result;
                }

                // Validate all grades
                var validationErrors = new List<string>();
                var validGrades = new List<GradeRecord>();

                foreach (var grade in grades)
                {
                    var error = await ValidateGradeRecord(grade, lecturer);
                    if (error != null)
                    {
                        validationErrors.Add(error);
                    }
                    else
                    {
                        validGrades.Add(grade);
                    }
                }

                if (validGrades.Count == 0)
                {
                    result.Success = false;
                    result.Message = "No valid grades to upload";
                    result.Errors = validationErrors;
                    return result;
                }

                // Store for approval (not yet saved to database)
                result.Success = true;
                result.Message = $"Processed {validGrades.Count} grades successfully";
                result.Grades = validGrades;
                result.ErrorCount = validationErrors.Count;
                result.Errors = validationErrors;

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error processing file: {ex.Message}";
                return result;
            }
        }

        // ========== VALIDATE GRADE RECORD ==========
        private async Task<string?> ValidateGradeRecord(GradeRecord grade, Lecturer lecturer)
        {
            try
            {
                // Check student exists
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.RegNumber == grade.RegNumber);

                if (student == null)
                    return $"Row {grade.RowNumber}: Student {grade.RegNumber} not found";

                // Check course exists
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.CourseCode == grade.CourseCode);

                if (course == null)
                    return $"Row {grade.RowNumber}: Course {grade.CourseCode} not found";

                // Check lecturer teaches this course
                var lecturerCourse = await _context.LecturerCourses
                    .FirstOrDefaultAsync(lc => lc.LecturerId == lecturer.LecturerId
                        && lc.CourseId == course.CourseId);

                if (lecturerCourse == null)
                    return $"Row {grade.RowNumber}: You are not assigned to teach {grade.CourseCode}";

                // Check student is enrolled
                var enrollment = await _context.CourseEnrollments
                    .FirstOrDefaultAsync(e => e.RegNumber == grade.RegNumber
                        && e.CourseId == course.CourseId);

                if (enrollment == null)
                    return $"Row {grade.RowNumber}: Student not enrolled in {grade.CourseCode}";

                // Validate marks are in range (0-100)
                if (grade.CourseworkMark.HasValue && (grade.CourseworkMark < 0 || grade.CourseworkMark > 100))
                    return $"Row {grade.RowNumber}: Coursework mark must be 0-100";

                if (grade.TestMark.HasValue && (grade.TestMark < 0 || grade.TestMark > 100))
                    return $"Row {grade.RowNumber}: Test mark must be 0-100";

                if (grade.FinalExamMark.HasValue && (grade.FinalExamMark < 0 || grade.FinalExamMark > 100))
                    return $"Row {grade.RowNumber}: Final exam mark must be 0-100";

                // Calculate final mark if all components provided
                if (grade.CourseworkMark.HasValue && grade.TestMark.HasValue && grade.FinalExamMark.HasValue)
                {
                    grade.CalculatedMark = CalculateFinalMark(
                        grade.CourseworkMark.Value,
                        grade.TestMark.Value,
                        grade.FinalExamMark.Value,
                        grade.CourseworkWeight,
                        grade.TestWeight,
                        grade.FinalExamWeight
                    );

                    // Validate calculated mark matches provided mark
                    if (grade.Mark.HasValue && Math.Abs(grade.Mark.Value - grade.CalculatedMark.Value) > 0.5m)
                        return $"Row {grade.RowNumber}: Provided mark {grade.Mark} doesn't match calculated mark {grade.CalculatedMark}";

                    grade.Mark = grade.CalculatedMark;
                }

                // Validate mark is provided
                if (!grade.Mark.HasValue)
                    return $"Row {grade.RowNumber}: Mark is required";

                // Validate mark range
                if (grade.Mark < 0 || grade.Mark > 100)
                    return $"Row {grade.RowNumber}: Mark must be 0-100";

                // Determine grade
                grade.Grade = GetGrade(grade.Mark.Value);
                grade.GradePoint = GetGradePoint(grade.Grade);

                // Add course details
                grade.CourseName = course.CourseName;
                grade.CourseId = course.CourseId;
                grade.CreditHours = course.CreditHours;

                return null; // Valid
            }
            catch (Exception ex)
            {
                return $"Row {grade.RowNumber}: Validation error - {ex.Message}";
            }
        }

        // ========== CALCULATE FINAL MARK ==========
        private static decimal CalculateFinalMark(decimal coursework, decimal test, decimal finalExam,
            int cwWeight, int testWeight, int examWeight)
        {
            decimal cwContribution = (coursework * cwWeight) / 100m;
            decimal testContribution = (test * testWeight) / 100m;
            decimal examContribution = (finalExam * examWeight) / 100m;

            return Math.Round(cwContribution + testContribution + examContribution, 2);
        }

        // ========== PARSE CSV FILE ==========
        private static List<GradeRecord> ParseCsvFile(Stream fileStream)
        {
            var grades = new List<GradeRecord>();
            using var reader = new StreamReader(fileStream);

            string? line;
            int rowNumber = 2; // Skip header
            bool isHeader = true;

            while ((line = reader.ReadLine()) != null)
            {
                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }

                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    var grade = new GradeRecord
                    {
                        RowNumber = rowNumber,
                        RegNumber = parts[0].Trim(),
                        CourseCode = parts[1].Trim(),
                        CourseworkWeight = 20,
                        TestWeight = 20,
                        FinalExamWeight = 60
                    };

                    // Parse marks
                    if (decimal.TryParse(parts[2].Trim(), out var mark))
                        grade.Mark = mark;

                    // Optional: Coursework, Test, Exam (if provided)
                    if (parts.Length > 3 && decimal.TryParse(parts[3].Trim(), out var cw))
                        grade.CourseworkMark = cw;
                    if (parts.Length > 4 && decimal.TryParse(parts[4].Trim(), out var test))
                        grade.TestMark = test;
                    if (parts.Length > 5 && decimal.TryParse(parts[5].Trim(), out var exam))
                        grade.FinalExamMark = exam;

                    grades.Add(grade);
                }
                rowNumber++;
            }

            return grades;
        }

        // ========== PARSE EXCEL FILE ==========
        private static List<GradeRecord> ParseExcelFile()
        {
            var grades = new List<GradeRecord>();
            // Note: You'll need to install: Install-Package ClosedXML
            // For now, returning empty list - implement with ClosedXML when needed
            return grades;
        }
    }

    // ========== DTOs ==========
    public class GradeRecord
    {
        public int RowNumber { get; set; }
        public string? RegNumber { get; set; }
        public string? CourseCode { get; set; }
        public string? CourseName { get; set; }
        public int CourseId { get; set; }
        public int? CreditHours { get; set; }
        public decimal? Mark { get; set; }
        public string? Grade { get; set; }
        public decimal? GradePoint { get; set; }
        public decimal? CourseworkMark { get; set; }
        public int CourseworkWeight { get; set; } = 20;
        public decimal? TestMark { get; set; }
        public int TestWeight { get; set; } = 20;
        public decimal? FinalExamMark { get; set; }
        public int FinalExamWeight { get; set; } = 60;
        public decimal? CalculatedMark { get; set; }
    }

    public class GradeUploadResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<GradeRecord>? Grades { get; set; }
        public int ErrorCount { get; set; }
        public List<string>? Errors { get; set; }
    }
}