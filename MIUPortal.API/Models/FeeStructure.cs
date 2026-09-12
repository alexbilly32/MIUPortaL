using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("fee_structures")]
    public class FeeStructure
    {
        [Key]
        public int FeeStructureId { get; set; }

        [Required]
        [StringLength(10)]
        public required string ProgrammeCode { get; set; }
        [ForeignKey(nameof(ProgrammeCode))]
        public virtual Programme? Programme { get; set; }

        // e.g. "2026/2027"
        [Required]
        [StringLength(20)]
        public required string AcademicYear { get; set; }

        // 1 or 2
        [Required]
        public int Semester { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal Tuition { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal FunctionalFees { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OtherCharges { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}