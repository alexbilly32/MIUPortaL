using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("Students")]
    public class Student
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int StudentId { get; set; }

        [Required]
        [MaxLength(50)]
        public string? RegNumber { get; set; }

        [Required]
        [MaxLength(100)]
        public string? Email { get; set; }

        [Required]
        [MaxLength(50)]
        public string? FirstName { get; set; }

        [MaxLength(50)]
        public string? MiddleName { get; set; }

        [Required]
        [MaxLength(50)]
        public string? LastName { get; set; }

        [MaxLength(255)]
        public string? PasswordHash { get; set; }

        public DateTime? DateOfBirth { get; set; }
        public DateTime? GraduationDate { get; set; }

        [MaxLength(20)]
        public string? Gender { get; set; }

        // ✅ FIXED: Made nullable to prevent "doesn't have a default value" error
        [MaxLength(20)]
        public string? PrimaryPhone { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(20)]
        public string? WhatsappPhone { get; set; }

        [MaxLength(20)]
        public string? NationalId { get; set; }

        [MaxLength(100)]
        public string? Nationality { get; set; }

        [MaxLength(50)]
        public string? Religion { get; set; }

        [MaxLength(50)]
        public string? MaritalStatus { get; set; }

        [MaxLength(10)]
        public string? ProgrammeCode { get; set; }

        [MaxLength(100)]
        public string? ProgrammeName { get; set; }

        [MaxLength(50)]
        public string? IntakeMonth { get; set; }

        // Academic Year of Admission
        [MaxLength(20)]
        public string? AcademicYear { get; set; }

        // Semester admitted into
        public int? EntrySemester { get; set; }

        public string? PhotoPath { get; set; }

        // Campus Name (optional)
        [MaxLength(50)]
        public string? Campus { get; set; }

        // Campus Code
        [MaxLength(20)]
        public string? CampusCode { get; set; }

        [MaxLength(50)]
        public string? StudyMode { get; set; }

        public DateTime? DateAdmitted { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public int? CurrentYear { get; set; }

        public int? CurrentSemester { get; set; }

        public bool ReadyForPromotion { get; set; } = false;

        // ===== FINANCIAL FIELDS =====
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? FeesBalance { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? TotalPaid { get; set; }

        // ===== PASSWORD RESET =====
        [MaxLength(255)]
        public string? PasswordResetToken { get; set; }

        public DateTime? PasswordResetExpiry { get; set; }

        // ===== TIMESTAMPS =====
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
        [MaxLength(50)]
        public string? DegreeClass { get; set; }

        public decimal? CGPA { get; set; }

        public string? PassportPhotoPath { get; set; }

        public string FeeCategory { get; set; } = "SelfSponsorship";
        public bool LoanSchemeApproved { get; set; } = false;

        // ===== NAVIGATION PROPERTIES (Reference existing models) =====
        public virtual ICollection<Payment>? Payments { get; set; }
        public virtual ICollection<FinancialTransaction>? FinancialTransactions { get; set; }
        public virtual ICollection<SemesterRegistration>? SemesterRegistrations { get; set; }
        public virtual ICollection<Enrollment>? CourseEnrollments { get; set; }
        public virtual ICollection<Result>? Results { get; set; }
        public virtual ICollection<Notification>? Notifications { get; set; }
        public virtual ICollection<Document>? Documents { get; set; }
    }
}