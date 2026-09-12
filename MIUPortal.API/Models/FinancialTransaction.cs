using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("FinancialLedger")]
    public class FinancialTransaction
    {
        [Key]
        public int LedgerId { get; set; }

        [Required]
        [StringLength(30)]
        public required string RegNumber { get; set; }

        // Allowed values: "CHARGE", "PAYMENT", "REVERSAL_PAYMENT", "REVERSAL_CHARGE"
        [StringLength(50)]
        public string? TransactionType { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [StringLength(50)]
        public string? PaymentMethod { get; set; }
        [StringLength(100)]
        public string? PaymentReference { get; set; }
        [StringLength(255)]
        public string? ReceiptPath { get; set; }
        public DateTime? TransactionDate { get; set; }
        [StringLength(50)]
        public string? Status { get; set; } = "COMPLETED";
        public string? Notes { get; set; }

        // Links this ledger row to the Payment it came from, when applicable.
        public int? PaymentId { get; set; }
        [ForeignKey(nameof(PaymentId))]
        public virtual Payment? Payment { get; set; }

        // Self-reference: a REVERSAL_* row points back at the ledger entry it reverses.
        public int? RelatedLedgerId { get; set; }
        [ForeignKey(nameof(RelatedLedgerId))]
        public virtual FinancialTransaction? RelatedLedgerEntry { get; set; }

        // Who recorded this entry from the Bursar Portal. Null for student self-service payments.
        public int? BursarId { get; set; }
        [ForeignKey(nameof(BursarId))]
        public virtual Bursar? Bursar { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(RegNumber))]
        public virtual Student? Student { get; set; }
    }
}