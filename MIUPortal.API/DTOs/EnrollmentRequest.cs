namespace MIUPortal.API.DTOs
{
    public class EnrollmentRequest
    {
        public string? RegNumber { get; set; }
        public string? CourseCode { get; set; }
        public int? Year { get; set; }
        public int? SemesterId { get; set; }
        public string? Status { get; set; }
    }
}