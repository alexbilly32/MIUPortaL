using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace MIUPortal.API.Models
{
    [Table("documents")]
    public class Document
    {
        public int DocumentId { get; set; }
        [Column("RegNumber")]        // ✅ CORRECT: Database has "RegNumber"
        [StringLength(30)]
        public string? RegNumber { get; set; }
        public int DocumentTypeId { get; set; }
        [ForeignKey(nameof(DocumentTypeId))]
        public virtual DocumentType? DocumentTypeNavigation { get; set; }
        [StringLength(50)]
        public string? DocumentType { get; set; }
        [Column("DocumentPath")]     // ✅ CORRECT: Database has "DocumentPath"
        [StringLength(255)]
        public string? DocumentPath { get; set; }
        public string? QrCodePath { get; set; }
        public string? Status { get; set; } = "Pending";
        public DateTime? DateGenerated { get; set; }
        public DateTime? DateDownloaded { get; set; }
        public decimal DocumentFee { get; set; } = 50000;
        public bool FeePaid { get; set; } = false;
        public string? QrVerificationCode { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? GeneratedByAdminId { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; } = DateTime.Now;
        [ForeignKey("RegNumber")]
        public virtual Student? Student { get; set; }
        public string? VerificationId { get; set; }
        public string? DocumentNumber { get; set; }

        public string? VerificationHash { get; set; }
        public bool IsRevoked { get; set; } = false;
        public DateTime? RevokedAt { get; set; }
        public int VerificationCount { get; set; } = 0;
        public DateTime? LastVerifiedAt { get; set; }

        // ✅ NEW: links a payment receipt Document back to the Payment that
        // generated it. Nullable because only PAYMENT_RECEIPT documents use it —
        // every other document type (admission letter, exam permit, etc.) leaves this null.
        public int? PaymentId { get; set; }

        [ForeignKey(nameof(PaymentId))]
        public virtual Payment? Payment { get; set; }
    }
}