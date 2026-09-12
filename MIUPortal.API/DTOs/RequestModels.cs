namespace MIUPortal.API.DTOs
{
    // Authentication Requests
    // 🛠️ Changed from LoginRequest to StudentLoginRequest to match your controller
    public class StudentLoginRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    public class OtpVerificationRequest
    {
        public string? Email { get; set; }
        public string? OtpCode { get; set; }
    }



    // 🛠️ Added missing ForgotPasswordRequest class
    public class ForgotPasswordRequest
    {
        public string? Email { get; set; }
    }

    // 🛠️ Added missing ResetPasswordRequest class
    public class ResetPasswordRequest
    {
        public string? Token { get; set; }
        public string? NewPassword { get; set; }
    }

    // Student Requests
    public class StudentProfileUpdateRequest
    {
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? WhatsappPhone { get; set; }
        public string? Email { get; set; }
    }

    public class CourseEnrollmentRequest
    {
        public string? RegNumber { get; set; }
        public int CourseId { get; set; }
        public int Year { get; set; }
        public int Semester { get; set; }
    }

    // Payment Requests
    public class MakePaymentRequest
    {
        public string? RegNumber { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Network { get; set; }
    }

    // Application Requests
    public class RegisterRequest
    {
        // ==========================
        // PERSONAL INFORMATION
        // ==========================
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? Gender { get; set; }

        public string? Nationality { get; set; }

        // NEW
        public string? IdentificationType { get; set; }

        public string? NationalId { get; set; }

        public string? PassportNumber { get; set; }

        public string? Religion { get; set; }

        public string? MaritalStatus { get; set; }

        // ==========================
        // CONTACT INFORMATION
        // ==========================
        public string? Email { get; set; }

        public string? PrimaryPhone { get; set; }

        public string? WhatsappPhone { get; set; }

        public string? District { get; set; }

        public string? PhysicalAddress { get; set; }

        // ==========================
        // EMERGENCY CONTACT
        // ==========================
        public string? EmergencyContactName { get; set; }

        public string? EmergencyRelationship { get; set; }

        public string? EmergencyPhone { get; set; }

        // ==========================
        // ACADEMIC INFORMATION
        // ==========================
        

        public string? ProgrammeCode { get; set; }

        public string? ProgrammeName { get; set; }

        // Selected campus
        public string? CampusCode { get; set; }

        // Academic year (e.g. 2026/2027)
        public string? AcademicYear { get; set; }

        // Semester of entry
        public int? EntrySemester { get; set; }

        // Intake (January / May / August)
        public string? Intake { get; set; }

        // Keep temporarily for compatibility if existing code still references it
        public int? YearOfEntry { get; set; }

        public string? StudyMode { get; set; }

        // ==========================
        // PREVIOUS EDUCATION
        // ==========================
        public string? PreviousSchool { get; set; }

        public int? YearOfCompletion { get; set; }

        public string? CertificateObtained { get; set; }

        public string? SubjectsPassed { get; set; }

        // ==========================
        // ACCOUNT
        // ==========================
        public string? Password { get; set; }


        // ==========================
        // PAYMENT
        // ==========================
        public decimal? PaymentAmount { get; set; }

        public string? PaymentProofPath { get; set; }
        public string? PassportPhotoPath { get; set; }


        public string? ApplicationCategory { get; set; } // "GovernmentLoan" | "UniversityBursary" | "SelfSponsorship"

        public bool AgreementAccepted { get; set; }
    }
}