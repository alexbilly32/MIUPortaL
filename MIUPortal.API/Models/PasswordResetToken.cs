using System.ComponentModel.DataAnnotations;

namespace MIUPortal.API.Models
{
    public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string UserRole { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string ResetToken { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiryTime { get; set; }

        public bool Used { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}