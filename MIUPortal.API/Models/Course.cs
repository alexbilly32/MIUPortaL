using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("Courses")]
    public class Course
    {
        [Key]
        public int CourseId { get; set; }

        [Required]
        public string CourseCode { get; set; } = string.Empty;

        [Required]
        public string CourseName { get; set; } = string.Empty;

        [Required]
        public string ProgrammeCode { get; set; } = string.Empty;

        [Required]
        public int Year { get; set; }

        [Required]
        public int Semester { get; set; }

        [Required]
        public int CreditHours { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Prerequisites { get; set; }

        public int? AssignedLecturerId { get; set; }

        public bool? IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<Enrollment>? CourseEnrollments { get; set; }
        public int? FacultyId { get; set; }
        public bool IsCrossFacultyModule { get; set; } = false;

        [ForeignKey("FacultyId")]
        public virtual Faculty? Faculty { get; set; }

        [ForeignKey(nameof(ProgrammeCode))]
        public virtual Programme Programme { get; set; } = null!;



    }
}