using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("lecturers")]
    public class Lecturer
    {
        [Key]
        public int LecturerId { get; set; }

        [Required]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public bool MustChangePassword { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        public int? FacultyId { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("FacultyId")]
        public virtual Faculty? Faculty { get; set; }

        public virtual ICollection<LecturerCourse> LecturerCourses { get; set; } = [];

        [StringLength(255)]
        public string? PasswordResetToken { get; set; }

        public DateTime? PasswordResetExpiry { get; set; }
        public bool IsDean { get; set; } = false;
        public DateTime? DeanAssignedDate { get; set; }
    }
}