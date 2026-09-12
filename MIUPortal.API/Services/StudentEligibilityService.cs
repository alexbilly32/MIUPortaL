using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Utilities;

namespace MIUPortal.API.Services
{
    public class SemesterEligibilityResult
    {
        public bool IsEligible { get; set; }
        public decimal TuitionPaid { get; set; }
        public decimal TuitionRequired { get; set; }
        public decimal FunctionalPaid { get; set; }
        public decimal FunctionalRequired { get; set; }
    }

  
    public class StudentEligibilityService
    {
        private readonly MIUContext _context;
        private const decimal FUNCTIONAL_FEES_PER_YEAR = 205000m;

        public StudentEligibilityService(MIUContext context)
        {
            _context = context;
        }

        public async Task<SemesterEligibilityResult> CheckEligibility(string regNumber, string programmeCode, int semesterNumber)
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
            if (student == null)
            {
                return new SemesterEligibilityResult { IsEligible = false };
            }

            var programme = await _context.Programmes
                .FirstOrDefaultAsync(p => p.ProgrammeCode == programmeCode);

            decimal tuitionRequired = FeeCategoryHelper.GetRequiredTuition(student, programme?.TuitionFeePerSemester ?? 0m);

            decimal functionalRequired = semesterNumber == 1
                ? FUNCTIONAL_FEES_PER_YEAR * 0.5m
                : FUNCTIONAL_FEES_PER_YEAR;

            decimal tuitionPaid = await _context.Payments
                .Where(p => p.RegNumber == regNumber && p.PaymentCategory == "TUITION" && p.Status == "Approved")
                .SumAsync(p => p.Amount);

            decimal functionalPaid = await _context.Payments
                .Where(p => p.RegNumber == regNumber && p.PaymentCategory == "FUNCTIONAL" && p.Status == "Approved")
                .SumAsync(p => p.Amount);

            bool eligible = tuitionRequired >= 0
                && tuitionPaid >= tuitionRequired
                && functionalPaid >= functionalRequired;

            return new SemesterEligibilityResult
            {
                IsEligible = eligible,
                TuitionPaid = tuitionPaid,
                TuitionRequired = tuitionRequired,
                FunctionalPaid = functionalPaid,
                FunctionalRequired = functionalRequired
            };
        }
    }
}