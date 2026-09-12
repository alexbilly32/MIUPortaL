namespace MIUPortal.API.Models
{
    public class Admin
    {
        public int AdminId { get; set; }
        public string? AdminCode { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Role { get; set; }
        public string? CampusCode { get; set; }
        public string? PasswordHash { get; set; }
        public string? OtpCode { get; set; }

        public DateTime? OtpExpiry { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string? PasswordResetToken { get; set; }

        public DateTime? PasswordResetExpiry { get; set; }
    }
}
