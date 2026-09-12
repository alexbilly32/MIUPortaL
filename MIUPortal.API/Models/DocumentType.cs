using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("documenttypes")]
    public class DocumentType
    {
        [Key]
        public int DocumentTypeId { get; set; }

        [Column("DocumentType")]
        public string DocumentTypeName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal DocumentFee { get; set; }

        public bool IsRequired { get; set; }

        public bool IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}