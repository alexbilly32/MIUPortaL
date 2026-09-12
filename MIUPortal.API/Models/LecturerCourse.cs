using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("lecturer_courses")]
    public class LecturerCourse
    {
        [Key]
        public int LecturerCourseId { get; set; }

        [Required]
        public int LecturerId { get; set; }

        [Required]
        public int CourseId { get; set; }

        public int? AcademicYear { get; set; }

        public int? Semester { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("LecturerId")]
        public virtual Lecturer? Lecturer { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course? Course { get; set; }
    }
}