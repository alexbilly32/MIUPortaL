using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("payments")]
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }


        [Required]
        [StringLength(30)]
        public required string RegNumber { get; set; }


        [Required]
        public decimal Amount { get; set; }


        public DateTime PaymentDate { get; set; } = DateTime.Now;


        [StringLength(50)]
        public string? PaymentMethod { get; set; }


        [StringLength(20)]
        public string? Network { get; set; }


        [StringLength(20)]
        public string? PhoneNumber { get; set; }


        [StringLength(20)]
        public string? Status { get; set; } = "Pending";


        [StringLength(100)]
        public string? TransactionReference { get; set; }


        [StringLength(255)]
        public string? ReceiptPath { get; set; }


        public bool ReceiptGenerated { get; set; } = false;


        [StringLength(255)]
        public string? Description { get; set; }


        public int? ProcessedByAdminId { get; set; }
        public int? ProcessedByBursarId { get; set; }
        [ForeignKey(nameof(ProcessedByBursarId))]
        public virtual Bursar? ProcessedByBursar { get; set; }


        public DateTime? CreatedAt { get; set; } = DateTime.Now;


        public DateTime? UpdatedAt { get; set; } = DateTime.Now;



        // ==================================
        // PAYMENT TYPE
        // ==================================

        [StringLength(50)]
        public string PaymentType { get; set; } = "TUITION_FEE";



        // ==================================
        // PAYMENT CATEGORY
        // TUITION / FUNCTIONAL
        // ==================================

        [StringLength(20)]
        public string PaymentCategory { get; set; } = "TUITION";


        public int? SemesterId { get; set; }



        // ==================================
        // STUDENT RELATIONSHIP
        // ==================================

        [ForeignKey(nameof(RegNumber))]
        public virtual Student? Student { get; set; }



        // ==================================
        // DOCUMENT RELATIONSHIP (NEW)
        // 
        // One payment can have documents
        // Example:
        // Payment Receipt PDF
        // ==================================

        public virtual ICollection<Document>? Documents { get; set; }

        [StringLength(255)]
        public string? PaymentProofPath { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        public int? ReviewedByBursarId { get; set; }
        [ForeignKey(nameof(ReviewedByBursarId))]
        public virtual Bursar? ReviewedByBursar { get; set; }

        public DateTime? ReviewedAt { get; set; }

    }
}