using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("schools")]
    public class School
    {
        [Key]
        public int SchoolId { get; set; }

        [Required]
        [StringLength(150)]
        public string SchoolName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string SchoolCode { get; set; } = string.Empty;

        [Required]
        public int FacultyId { get; set; }

        public string? Description { get; set; }

        public DateTime? CreatedAt { get; set; }

        // Navigation Property
        [ForeignKey(nameof(FacultyId))]
        public virtual Faculty Faculty { get; set; } = null!;

        // One School has many Programmes
        public virtual ICollection<Programme> Programmes { get; set; }
            = new List<Programme>();
    }
}