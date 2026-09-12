using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("SemesterRegistration")]
    public class SemesterRegistration
    {
        [Key]
        public int RegistrationId { get; set; }

        [ForeignKey("Student")]
        public string? RegNumber { get; set; }

        public string? Semester { get; set; }
        public int AcademicYear { get; set; }
        public int RegisteredCourseCount { get; set; }
        public decimal FeePaid { get; set; }
        public decimal TotalFees { get; set; }
        public int PercentagePaid { get; set; }
        public string? RegistrationCardPath { get; set; }
        public string? ExamPermitPath { get; set; }
        public string? Status { get; set; }
        public DateTime RegistrationDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // NAVIGATION PROPERTIES
        public virtual Student? Student { get; set; }
    }
}
