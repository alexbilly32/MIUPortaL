using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Utilities;

namespace MIUPortal.API.Services
{
    public class PeriodPayments
    {
        public decimal TuitionPaidThisSemester { get; set; }
        public decimal FunctionalPaidThisYear { get; set; }
    }

    public class PeriodClearance
    {
        public int Year { get; set; }
        public int Semester { get; set; }
        public decimal TuitionRequired { get; set; }
        public decimal TuitionPaid { get; set; }
        public bool TuitionCleared { get; set; }
        public decimal FunctionalRequired { get; set; }
        public decimal FunctionalPaidThisYear { get; set; }
        public bool FunctionalCleared { get; set; }
        public bool IsCleared => TuitionCleared && FunctionalCleared;
    }

    // =========================================================
    // REWRITTEN to read the student's personal year/semester -> global
    // SemesterId mapping from StudentSemesterEnrollment instead of
    // re-deriving it from DateAdmitted + elapsed-semester math on every
    // call. This is what makes fee scoping stay correct once promotion
    // becomes completion-driven instead of date-driven — a student who
    // was held back, or promoted mid-cycle, no longer silently falls out
    // of sync with SemesterMathHelper's assumption that everyone
    // advances in lockstep with the calendar.
    //
    // Rule (unchanged from before):
    //   TUITION resets every semester — only payments tagged to the
    //   student's CURRENT global semester count.
    //   FUNCTIONAL FEES accumulate across BOTH semesters of the student's
    //   CURRENT personal year, resetting only when the year changes.
    // =========================================================
    public class FeeScopingService
    {
        private readonly MIUContext _context;
        private const decimal FUNCTIONAL_FEES_PER_YEAR = 205000m;

        public FeeScopingService(MIUContext context)
        {
            _context = context;
        }

        public async Task<PeriodPayments> GetScopedPaymentsAsync(Student student)
        {
            var enrollments = await _context.StudentSemesterEnrollments
                .Where(e => e.RegNumber == student.RegNumber)
                .ToListAsync();

            var current = enrollments.FirstOrDefault(e => e.IsCurrent);
            if (current == null)
            {
                // No enrollment row yet — either the backfill hasn't run,
                // or this is a brand-new student whose first enrollment
                // row hasn't been created at admission approval. Return
                // zero rather than guessing.
                return new PeriodPayments { TuitionPaidThisSemester = 0, FunctionalPaidThisYear = 0 };
            }

            var semesterIdsThisYear = enrollments
                .Where(e => e.PersonalYear == current.PersonalYear)
                .Select(e => e.SemesterId)
                .Distinct()
                .ToList();

            int currentSemesterId = current.SemesterId;

            decimal tuitionPaidThisSemester = await _context.Payments
                .Where(p => p.RegNumber == student.RegNumber &&
                            p.PaymentCategory == "TUITION" &&
                            p.Status == "Approved" &&
                            p.SemesterId == currentSemesterId)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            decimal functionalPaidThisYear = semesterIdsThisYear.Count == 0
                ? 0
                : await _context.Payments
                    .Where(p => p.RegNumber == student.RegNumber &&
                                p.PaymentCategory == "FUNCTIONAL" &&
                                p.Status == "Approved" &&
                                p.SemesterId != null &&
                                semesterIdsThisYear.Contains(p.SemesterId.Value))
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;

            return new PeriodPayments
            {
                TuitionPaidThisSemester = tuitionPaidThisSemester,
                FunctionalPaidThisYear = functionalPaidThisYear
            };
        }

        // =========================================================
        // Evaluates fee clearance INDEPENDENTLY for every (year, semester)
        // the student has ever actually reached — read straight from
        // their enrollment history rather than recomputed from admission
        // date. This is what GpaController's results-visibility gating
        // needs: a student who falls behind on THIS semester's tuition
        // should not lose access to already-earned, already-paid-for
        // results from previous years.
        // =========================================================
        public async Task<List<PeriodClearance>> GetAllPeriodClearanceAsync(Student student)
        {
            var programme = await _context.Programmes
                .FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);
            decimal tuitionRequiredPerSemester = FeeCategoryHelper.GetRequiredTuition(student, programme?.TuitionFeePerSemester ?? 0m);

            var enrollments = await _context.StudentSemesterEnrollments
                .Where(e => e.RegNumber == student.RegNumber)
                .OrderBy(e => e.PersonalYear)
                .ThenBy(e => e.PersonalSemester)
                .ToListAsync();

            if (enrollments.Count == 0) return new List<PeriodClearance>();

            // Functional fees accumulate across both semesters of a
            // personal year, so group global semester IDs by year first.
            var byYear = enrollments
                .GroupBy(e => e.PersonalYear)
                .ToDictionary(g => g.Key, g => g.Select(e => e.SemesterId).ToList());

            var allPayments = await _context.Payments
                .Where(p => p.RegNumber == student.RegNumber && p.Status == "Approved" && p.SemesterId != null)
                .Select(p => new { p.SemesterId, p.PaymentCategory, p.Amount })
                .ToListAsync();

            var result = new List<PeriodClearance>();

            foreach (var enrollment in enrollments)
            {
                int year = enrollment.PersonalYear;
                int semester = enrollment.PersonalSemester;
                int thisSemesterId = enrollment.SemesterId;
                var thisYearIds = byYear[year];

                decimal tuitionPaid = allPayments
                    .Where(p => p.PaymentCategory == "TUITION" && p.SemesterId == thisSemesterId)
                    .Sum(p => p.Amount);

                decimal functionalPaidThisYear = allPayments
                    .Where(p => p.PaymentCategory == "FUNCTIONAL" && p.SemesterId.HasValue && thisYearIds.Contains(p.SemesterId.Value))
                    .Sum(p => p.Amount);

                decimal functionalRequired = semester == 1
                    ? FUNCTIONAL_FEES_PER_YEAR * 0.5m
                    : FUNCTIONAL_FEES_PER_YEAR;

                result.Add(new PeriodClearance
                {
                    Year = year,
                    Semester = semester,
                    TuitionRequired = tuitionRequiredPerSemester,
                    TuitionPaid = tuitionPaid,
                    TuitionCleared = tuitionPaid >= tuitionRequiredPerSemester,
                    FunctionalRequired = functionalRequired,
                    FunctionalPaidThisYear = functionalPaidThisYear,
                    FunctionalCleared = functionalPaidThisYear >= functionalRequired
                });
            }

            return result;
        }
    }
}