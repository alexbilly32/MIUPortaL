using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("programmes")]
    public class Programme
    {
        [Key]
        [StringLength(10)]
        public string ProgrammeCode { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string ProgrammeName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int DurationYears { get; set; } = 3;

        public decimal TuitionFeePerSemester { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        [StringLength(100)]
        public string? ProgrammeLeader { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? SchoolId { get; set; }

        [ForeignKey(nameof(SchoolId))]
        public virtual School School { get; set; } = null!;

        // Navigation
        public virtual ICollection<Course> Courses { get; set; }
            = new List<Course>();
    }
}