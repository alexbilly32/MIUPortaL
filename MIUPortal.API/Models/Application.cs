using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("Applications")]
    public class Application
    {
        [Key]
        public int ApplicationId { get; set; }

        public string? ApplicationNumber { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Nationality { get; set; }
        public string? NationalId { get; set; }

        public string? IdentificationType { get; set; }

        public string? PassportNumber { get; set; }
        public string? Religion { get; set; }
        public string? MaritalStatus { get; set; }
        public string? PrimaryPhone { get; set; }
        public string? WhatsappPhone { get; set; }
        public string? Email { get; set; }
        public string? District { get; set; }
        public string? PhysicalAddress { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyRelationship { get; set; }
        public string? EmergencyPhone { get; set; }
        public string? ProgrammeCode { get; set; }

        public string? ProgrammeName { get; set; }

        // Campus selected by applicant
        public string? CampusCode { get; set; }

        // (Keep temporarily for backward compatibility if other code still uses it)
        public string? CampusPreference { get; set; }

        // Intake (January / May / August)
        public string? Intake { get; set; }

        // Academic Year (e.g. 2026/2027)
        public string? AcademicYear { get; set; }

        // Semester of entry
        public int? EntrySemester { get; set; }

        // Old field (can be removed later if no longer needed)
        public int? YearOfEntry { get; set; }

        public string? StudyMode { get; set; }
        public string? PreviousSchool { get; set; }
        public int? YearOfCompletion { get; set; }
        public string? CertificateObtained { get; set; }
        public string? SubjectsPassed { get; set; }
        public string? PassportPhotoPath { get; set; }
        public string? NationalIdPath { get; set; }
        public string? OlevelCertificatePath { get; set; }
        public string? AlevelCertificatePath { get; set; }
        public string? OtherCertificatesPath { get; set; }
        public bool? AgreementAccepted { get; set; }
        public string? DigitalSignature { get; set; }
        public string? Status { get; set; }
        public DateTime? ApplicationDate { get; set; }
        public DateTime? ReviewDate { get; set; }
        public string? AdminNotes { get; set; }
        public int? ReviewedByAdminId { get; set; }
        public string? RejectionReason { get; set; }

        // OTP FIELDS
        public string? OtpCode { get; set; }
        public DateTime? OtpExpiry { get; set; }
        public DateTime? OtpVerifiedAt { get; set; }
        public string? PasswordHash { get; set; }

        // PASSWORD RESET
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetExpiry { get; set; }
        // PAYMENT FIELDS (PHASE 2A)
        public string? PaymentStatus { get; set; }
        public string? PaymentReference { get; set; }
        public string? PaymentProofPath { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int? PaymentAmount { get; set; }


        // APPLICATION STATUS FIELDS (PHASE 2A)
        public string? ApplicationStatus { get; set; }
        public DateTime? AdminReviewDate { get; set; }
        public int? AdminId { get; set; }

        // REGISTRATION FIELDS (PHASE 2A)
        public string? RegistrationNumber { get; set; }
        public string? AdmissionLetterPath { get; set; }
        public DateTime? ApprovalDate { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? ApprovedByRegistrarId { get; set; }

        public string ApplicationCategory { get; set; } = "SelfSponsorship";
        public string? LoanProofPath { get; set; }
        public string? LoanSchemeStatus { get; set; }
        public string? LoanReviewedBy { get; set; }
        public DateTime? LoanReviewedDate { get; set; }
        public string? LoanRejectionReason { get; set; }
    }
}
