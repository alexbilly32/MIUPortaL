using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("clearance_overrides")]
    public class ClearanceOverride
    {
        [Key]
        public int OverrideId { get; set; }

        [Required]
        [StringLength(30)]
        public required string RegNumber { get; set; }
        [ForeignKey(nameof(RegNumber))]
        public virtual Student? Student { get; set; }

        // "REGISTRATION" or "EXAM" (extendable to "GRADUATION" later)
        [Required]
        [StringLength(30)]
        public required string ClearanceType { get; set; }

        [StringLength(20)]
        public string? AcademicYear { get; set; }
        public int? Semester { get; set; }

        // True = bursar is granting clearance despite balance; false = bursar
        // is revoking clearance the computed logic would otherwise grant.
        public bool IsGranted { get; set; } = true;

        [Required]
        [StringLength(500)]
        public required string Reason { get; set; }

        public int? BursarId { get; set; }
        [ForeignKey(nameof(BursarId))]
        public virtual Bursar? Bursar { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; } = DateTime.Now;
    }
}