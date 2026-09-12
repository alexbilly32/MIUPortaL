using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    // =========================================================
    // This table is the missing "translation layer" between a student's
    // PERSONAL progress (Year 1 Sem 1, Year 2 Sem 2, etc.) and the
    // UNIVERSITY'S global calendar (AcademicSemester rows the registrar
    // creates). Every other service (fees, results visibility, course
    // availability) should resolve "what period is this student in, and
    // which global SemesterId does that map to" from THIS table — never
    // by re-deriving it from DateAdmitted + elapsed-semester math, which
    // silently breaks the moment a student is held back, promoted early,
    // or the registrar edits a semester's dates after the fact.
    //
    // Exactly one row per student has IsCurrent = true at any time.
    // A new row is written only at the moment a student is actually
    // promoted (see StudentPromotionService) — never recomputed on read.
    // =========================================================
    public class StudentSemesterEnrollment
    {
        // [Key] is required here because the column is named
        // "EnrollmentId", not "Id" or "StudentSemesterEnrollmentId" — EF
        // Core's convention-based primary key detection only recognizes
        // those two patterns, so without this attribute EF throws
        // InvalidOperationException at startup on the FIRST request that
        // touches MIUContext (not just ones related to promotion).
        [Key]
        public int EnrollmentId { get; set; }

        public string RegNumber { get; set; } = null!;

        // FK -> AcademicSemesters.SemesterId (the GLOBAL semester this
        // personal period maps to).
        public int SemesterId { get; set; }

        public int PersonalYear { get; set; }
        public int PersonalSemester { get; set; }

        public bool IsCurrent { get; set; }

        public DateTime PromotedAt { get; set; }

        // "Admission" (first row, created at admin approval),
        // "System" (automatic completion-based promotion),
        // or a registrar's username (manual override).
        public string PromotedBy { get; set; } = "System";

        // Without [ForeignKey("RegNumber")], EF can't tell that the
        // RegNumber scalar property above IS the foreign key for this
        // navigation. Student's primary key is "RegNumber" (not "Id"),
        // which doesn't match EF's shadow-property naming convention for
        // navigation FKs (it looks for "Student" + "RegNumber" combined,
        // i.e. a property literally named "StudentRegNumber"). Without
        // this hint, EF silently creates an invisible shadow property
        // called StudentRegNumber and tries to write to a column that
        // doesn't exist in the database, causing every INSERT to fail
        // with "Unknown column 'StudentRegNumber'".
        [ForeignKey("RegNumber")]
        public Student? Student { get; set; }

        public AcademicSemester? Semester { get; set; }
    }
}