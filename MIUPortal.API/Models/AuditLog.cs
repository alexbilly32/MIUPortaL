using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("auditlogs")]
    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        // ===== LEGACY ADMIN FIELDS (bulk upload logging) =====
        // Left as-is for AdminController compatibility. Null/0 on bursar-sourced rows.
        public int? AdminId { get; set; }
        [StringLength(100)]
        public string? AdminEmail { get; set; }
        [StringLength(255)]
        public string? FileName { get; set; }
        public int? RecordsProcessed { get; set; } = 0;
        public int? RecordsSuccessful { get; set; } = 0;
        public int? RecordsFailed { get; set; } = 0;
        public string? ErrorLog { get; set; }

        // ===== SHARED FIELDS =====
        // "Action" holds the ActionType (e.g. PAYMENT_APPROVED, CLEARANCE_OVERRIDE, BURSAR_LOGIN)
        [StringLength(100)]
        public string? Action { get; set; }

        // "Details" holds the human-readable description
        public string? Details { get; set; }

        public DateTime ActionDate { get; set; } = DateTime.Now;
        public DateTime? CreatedAt { get; set; }

        // ===== BURSAR / GENERAL AUDIT FIELDS =====
        public int? BursarId { get; set; }
        [ForeignKey(nameof(BursarId))]
        public virtual Bursar? Bursar { get; set; }

        [StringLength(30)]
        public string? RegNumber { get; set; }

        // Generic pointer to whatever was acted on, e.g. "Payment", "FeeStructure"
        [StringLength(50)]
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }

        [StringLength(45)]
        public string? IPAddress { get; set; }

        public int? AcademicRegistrarId { get; set; }
        public int? DeanLecturerId { get; set; }
    }
}