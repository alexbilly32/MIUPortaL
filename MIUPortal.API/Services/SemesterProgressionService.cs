using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Services
{
    public class SemesterActivationResult
    {
        public AcademicSemester Semester { get; set; } = null!;
        public int StudentsPromoted { get; set; }
    }

   
    public class SemesterProgressionService
    {
        private readonly MIUContext _context;
        private readonly StudentPromotionService _promotionService;
        private readonly ILogger<SemesterProgressionService> _logger;

        public SemesterProgressionService(
            MIUContext context,
            StudentPromotionService promotionService,
            ILogger<SemesterProgressionService> logger)
        {
            _context = context;
            _promotionService = promotionService;
            _logger = logger;
        }

        public async Task<SemesterActivationResult> ActivateSemesterAsync(string academicYear, int semesterNumber, DateTime startDate, DateTime endDate)
        {
            var existing = await _context.AcademicSemesters
                .FirstOrDefaultAsync(s => s.AcademicYear == academicYear && s.Semester == semesterNumber);

            AcademicSemester activated;
            if (existing != null)
            {
                existing.StartDate = startDate;
                existing.EndDate = endDate;
                activated = existing;
            }
            else
            {
                activated = new AcademicSemester
                {
                    AcademicYear = academicYear,
                    Semester = semesterNumber,
                    StartDate = startDate,
                    EndDate = endDate
                };
                _context.AcademicSemesters.Add(activated);
            }

            // Only one semester is ever "active" at a time.
            var allSemesters = await _context.AcademicSemesters.ToListAsync();
            foreach (var s in allSemesters) s.IsActive = false;
            activated.IsActive = true;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Semester {Year}/{Sem} activated ({Start:d} - {End:d}).",
                academicYear, semesterNumber, startDate, endDate);

           
            int promoted = await _promotionService.PromoteAllReadyStudentsAsync(activated.SemesterId);

            _logger.LogInformation(
                "Semester {Year}/{Sem} activation promoted {Count} previously-ready student(s).",
                academicYear, semesterNumber, promoted);

            return new SemesterActivationResult { Semester = activated, StudentsPromoted = promoted };
        }

        public async Task<AcademicSemester?> GetActiveSemesterAsync()
        {
            return await _context.AcademicSemesters.FirstOrDefaultAsync(s => s.IsActive);
        }
    }
}