using MIUPortal.API.Models;

namespace MIUPortal.API.Services
{
    // Pulls the "elapsed semesters since this student's entry" math out of
    // SemesterProgressionService so it can be reused for payment scoping
    // (tuition/functional fee tracking) without duplicating the logic a
    // third time.
    public static class SemesterMathHelper
    {
        public static (int year, int semester) FromElapsed(int semestersElapsed)
        {
            int year = (int)Math.Ceiling(semestersElapsed / 2.0);
            int semester = semestersElapsed % 2 == 1 ? 1 : 2;
            return (year, semester);
        }

        public static AcademicSemester? FindEntrySemester(DateTime? dateAdmitted, List<AcademicSemester> orderedSemesters)
        {
            if (dateAdmitted == null) return null;
            return orderedSemesters.FirstOrDefault(s => s.StartDate <= dateAdmitted && s.EndDate >= dateAdmitted)
                ?? orderedSemesters.FirstOrDefault(s => s.StartDate >= dateAdmitted);
        }
    }
}