using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("CourseEnrollments")]
    public class Enrollment
    {
        [Key]
        public int EnrollmentId { get; set; }

        [ForeignKey("Student")]
        public string? RegNumber { get; set; }

        [ForeignKey("Course")]
        public int CourseId { get; set; }

        public int? SemesterId { get; set; }

        [ForeignKey("SemesterId")]
        public virtual AcademicSemester? AcademicSemester { get; set; }

        public int? Mark { get; set; }
        public string? Grade { get; set; }
        public string? Status { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // NAVIGATION PROPERTIES
        public virtual Student? Student { get; set; }
        public virtual Course Course { get; set; } = null!;
    }
}