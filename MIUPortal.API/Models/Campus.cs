namespace MIUPortal.API.Models
{
    public class Campus
    {
        public string? CampusCode { get; set; }
        public string? CampusName { get; set; }
        public string? Location { get; set; }
        public string? City { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? MainContactPerson { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
