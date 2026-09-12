namespace MIUPortal.API.DTOs
{
    // Generic Response
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
    }

    // Authentication Responses
    public class LoginResponse
    {
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public StudentDto? Student { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class StudentDto
    {
        public string? RegNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? ProgrammeCode { get; set; }
        public int CurrentYear { get; set; }
        public int CurrentSemester { get; set; }
        public decimal FeesBalance { get; set; }
        public decimal CGPA { get; set; }
    }

    public class AdminDto
    {
        public int AdminId { get; set; }
        public string? AdminCode { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? Role { get; set; }
        public string? CampusCode { get; set; }
    }

    // Dashboard Data
    public class StudentDashboardData
    {
        public StudentDto? Student { get; set; }
        public decimal FeesBalance { get; set; }
        public int UnreadNotifications { get; set; }
        public List<object>? RecentResults { get; set; }
        public List<object>? CurrentCourses { get; set; }
    }

    public class AdminDashboardData
    {
        public AdminDto? Admin { get; set; }
        public int TotalStudents { get; set; }
        public int PendingApplications { get; set; }
        public decimal TotalFeesCollected { get; set; }
    }

    // Pagination
    public class PaginatedResponse<T>
    {
        public List<T>? Data { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
