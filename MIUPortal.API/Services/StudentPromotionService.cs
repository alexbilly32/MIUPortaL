using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Utilities;

namespace MIUPortal.API.Services
{
   
    public class StudentPromotionService
    {
        private readonly MIUContext _context;
        private readonly ILogger<StudentPromotionService> _logger;

        public StudentPromotionService(MIUContext context, ILogger<StudentPromotionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<StudentSemesterEnrollment?> GetCurrentEnrollmentAsync(string regNumber)
        {
            return await _context.StudentSemesterEnrollments
                .Include(e => e.Semester)
                .FirstOrDefaultAsync(e => e.RegNumber == regNumber && e.IsCurrent);
        }

        
        public async Task<bool> PromoteStudentIfCompleteAsync(string regNumber, int personalYear, int personalSemester)
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
            if (student == null) return false;

            var currentEnrollment = await GetCurrentEnrollmentAsync(regNumber);
            if (currentEnrollment == null)
            {
                _logger.LogWarning("Student {Reg} has no current enrollment row — run the backfill first.", regNumber);
                return false;
            }

           
            if (currentEnrollment.PersonalYear != personalYear || currentEnrollment.PersonalSemester != personalSemester)
                return false;

            bool isComplete = await IsStudentCompleteForCurrentPeriodAsync(student, currentEnrollment);
            if (!isComplete) return false;

            var target = await FindNextGlobalSemesterAsync(currentEnrollment.SemesterId);

            if (target == null)
            {
                
                student.ReadyForPromotion = true;
                student.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Student {Reg} completed Year {Y} Sem {S} — marked ReadyForPromotion, awaiting next semester activation.",
                    regNumber, personalYear, personalSemester);
                return false;
            }

            await ApplyPromotionAsync(student, currentEnrollment, target, "System");
            return true;
        }

        
        public async Task<int> PromoteAllReadyStudentsAsync(int newlyActivatedSemesterId)
        {
            var readyStudents = await _context.Students
                .Where(s => s.ReadyForPromotion && s.Status == "ACTIVE")
                .ToListAsync();

            int count = 0;
            foreach (var student in readyStudents)
            {
                var currentEnrollment = await GetCurrentEnrollmentAsync(student.RegNumber!);
                if (currentEnrollment == null) continue;

                var target = await FindNextGlobalSemesterAsync(currentEnrollment.SemesterId);

                
                if (target == null || target.SemesterId != newlyActivatedSemesterId) continue;

                await ApplyPromotionAsync(student, currentEnrollment, target, "System");
                count++;
            }

            return count;
        }

       
        public async Task<bool> ForcePromoteAsync(string regNumber, string registrarUsername)
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
            if (student == null) return false;

            var currentEnrollment = await GetCurrentEnrollmentAsync(regNumber);
            if (currentEnrollment == null) return false;

            var target = await FindNextGlobalSemesterAsync(currentEnrollment.SemesterId);
            if (target == null) return false;

            await ApplyPromotionAsync(student, currentEnrollment, target, registrarUsername);
            return true;
        }

        private async Task<bool> IsStudentCompleteForCurrentPeriodAsync(Student student, StudentSemesterEnrollment currentEnrollment)
        {
            string regNumber = student.RegNumber!;

           
            var requiredCourseIds = await _context.Courses
                .Where(c => c.IsActive == true
                            && c.ProgrammeCode == student.ProgrammeCode
                            && c.Year == currentEnrollment.PersonalYear
                            && c.Semester == currentEnrollment.PersonalSemester)
                .Select(c => c.CourseId)
                .ToListAsync();

            
            if (requiredCourseIds.Count == 0) return false;

            var registeredCourseIds = await _context.CourseEnrollments
                .Where(e => e.RegNumber == regNumber && e.SemesterId == currentEnrollment.SemesterId)
                .Select(e => e.CourseId)
                .ToListAsync();

            if (!requiredCourseIds.All(cid => registeredCourseIds.Contains(cid))) return false;

            
            var signedOffCourseIds = await _context.Results
                .Where(r => r.RegNumber == regNumber
                            && r.Year == currentEnrollment.PersonalYear
                            && r.Semester == currentEnrollment.PersonalSemester
                            && r.Status == "RegistrarSignedOff")
                .Select(r => r.CourseId)
                .Distinct()
                .ToListAsync();

           
            return requiredCourseIds.All(cid => signedOffCourseIds.Contains(cid));
        }

        private async Task<AcademicSemester?> FindNextGlobalSemesterAsync(int currentGlobalSemesterId)
        {
            var current = await _context.AcademicSemesters.FirstOrDefaultAsync(s => s.SemesterId == currentGlobalSemesterId);
            if (current?.StartDate == null) return null;

            return await _context.AcademicSemesters
                .Where(s => s.StartDate != null && s.StartDate > current.StartDate)
                .OrderBy(s => s.StartDate)
                .FirstOrDefaultAsync();
        }

        private async Task ApplyPromotionAsync(Student student, StudentSemesterEnrollment currentEnrollment, AcademicSemester target, string promotedBy)
        {
            currentEnrollment.IsCurrent = false;

            int nextPersonalYear = currentEnrollment.PersonalYear;
            int nextPersonalSemester;
            if (currentEnrollment.PersonalSemester == 1)
            {
                nextPersonalSemester = 2;
            }
            else
            {
                nextPersonalYear += 1;
                nextPersonalSemester = 1;
            }

           
            var existingRow = await _context.StudentSemesterEnrollments
                .FirstOrDefaultAsync(e => e.RegNumber == student.RegNumber
                                        && e.PersonalYear == nextPersonalYear
                                        && e.PersonalSemester == nextPersonalSemester);

            if (existingRow != null)
            {
                existingRow.SemesterId = target.SemesterId;
                existingRow.IsCurrent = true;
                existingRow.PromotedAt = DateTime.Now;
                existingRow.PromotedBy = promotedBy;

                _logger.LogWarning(
                    "ApplyPromotionAsync: reactivated existing enrollment row (EnrollmentId={Id}) for {Reg} at Year {Year} Sem {Sem} instead of creating a duplicate.",
                    existingRow.EnrollmentId, student.RegNumber, nextPersonalYear, nextPersonalSemester);
            }
            else
            {
                var newEnrollment = new StudentSemesterEnrollment
                {
                    RegNumber = student.RegNumber!,
                    SemesterId = target.SemesterId,
                    PersonalYear = nextPersonalYear,
                    PersonalSemester = nextPersonalSemester,
                    IsCurrent = true,
                    PromotedAt = DateTime.Now,
                    PromotedBy = promotedBy
                };
                _context.StudentSemesterEnrollments.Add(newEnrollment);
            }

           
            student.CurrentYear = nextPersonalYear;
            student.CurrentSemester = nextPersonalSemester;
            student.ReadyForPromotion = false;
            student.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Promoted {Reg} to personal Year {Year} Sem {Sem} (global semester {GlobalId}), by {By}.",
                student.RegNumber, nextPersonalYear, nextPersonalSemester, target.SemesterId, promotedBy);

          
        }

        
        public async Task<int> BackfillMissingEnrollmentsAsync()
        {
            var studentsWithoutEnrollment = await _context.Students
                .Where(s => s.RegNumber != null && s.DateAdmitted != null)
                .Where(s => !_context.StudentSemesterEnrollments.Any(e => e.RegNumber == s.RegNumber && e.IsCurrent))
                .ToListAsync();

            var orderedSemesters = await _context.AcademicSemesters
                .Where(s => s.StartDate != null)
                .OrderBy(s => s.StartDate)
                .ToListAsync();

            int seeded = 0;
            foreach (var student in studentsWithoutEnrollment)
            {
                var entrySemester = SemesterMathHelper.FindEntrySemester(student.DateAdmitted, orderedSemesters);
                if (entrySemester == null) continue;

                int entryIndex = orderedSemesters.FindIndex(s => s.SemesterId == entrySemester.SemesterId);
                int personalYear = student.CurrentYear ?? 1;
                int personalSemester = student.CurrentSemester ?? 1;
                int elapsed = (personalYear - 1) * 2 + personalSemester;
                int targetIndex = entryIndex + elapsed - 1;

                if (targetIndex < 0 || targetIndex >= orderedSemesters.Count) continue;
                var globalSemester = orderedSemesters[targetIndex];

                _context.StudentSemesterEnrollments.Add(new StudentSemesterEnrollment
                {
                    RegNumber = student.RegNumber!,
                    SemesterId = globalSemester.SemesterId,
                    PersonalYear = personalYear,
                    PersonalSemester = personalSemester,
                    IsCurrent = true,
                    PromotedAt = DateTime.Now,
                    PromotedBy = "Backfill"
                });
                seeded++;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Backfill seeded {Count} enrollment rows.", seeded);
            return seeded;
        }
    }
}