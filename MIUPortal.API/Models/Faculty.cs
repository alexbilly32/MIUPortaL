using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("faculties")]
    public class Faculty
    {
        [Key]
        public int FacultyId { get; set; }

        [Required]
        [StringLength(100)]
        public string FacultyName { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string FacultyCode { get; set; } = string.Empty;

        public string? Description { get; set; }

        [StringLength(100)]
        public string? HeadOfDepartment { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<Lecturer>? Lecturers { get; set; }
        public virtual ICollection<Course>? Courses { get; set; }
    }
}