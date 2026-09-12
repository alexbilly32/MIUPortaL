using MIUPortal.API.Models;

namespace MIUPortal.API.Utilities
{
    public static class FeeCategoryHelper
    {
        /// <summary>
        /// Returns the tuition actually owed for the semester, after applying
        /// the student's fee category. Functional fees are NEVER affected by
        /// this — they're computed identically for all categories wherever
        /// FUNCTIONAL_FEES_PER_YEAR is used.
        /// </summary>
        public static decimal GetRequiredTuition(Student student, decimal baseTuitionPerSemester)
        {
            return student.FeeCategory switch
            {
                "UniversityBursary" => baseTuitionPerSemester * 0.5m,
                "GovernmentLoan" => student.LoanSchemeApproved ? 0m : baseTuitionPerSemester,
                _ => baseTuitionPerSemester // SelfSponsorship, and any unrecognized value — fail safe to full fee
            };
        }
    }
}