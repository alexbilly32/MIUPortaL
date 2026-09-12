using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("notifications")]
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }
        public string? RegNumber { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }

        [StringLength(50)]
        public string? Status { get; set; } = "UNREAD";
        public string? Type { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime? DateSent { get; set; }
        public DateTime? DateRead { get; set; }
        public string? RelatedLink { get; set; }
        public int? SentByAdminId { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey("RegNumber")]
        public virtual Student? Student { get; set; }
    }
}
