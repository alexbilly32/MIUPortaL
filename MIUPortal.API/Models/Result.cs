using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("results")]
    public class Result
    {
        [Key]
        public int ResultId { get; set; }

        // ========== STUDENT REFERENCE ==========
        [Column("StudentRegNumber")]
        [StringLength(30)]
        public string? RegNumber { get; set; }

        // ========== COURSE REFERENCE ==========
        public int CourseId { get; set; }

        // ========== COURSE DETAILS (FROM DATABASE) ==========
        [Column("CourseCode")]
        [StringLength(20)]
        public string? CourseCode { get; set; }

        [Column("CourseName")]
        [StringLength(150)]
        public string? CourseName { get; set; }

        // ========== SEMESTER & YEAR ==========
        public int? SemesterId { get; set; }

        [Column("Semester")]
        public int? Semester { get; set; }

        [Column("Year")]
        public int? Year { get; set; }

        // ========== FINAL MARK & GRADE ==========
        [Column("Mark")]
        public decimal? Mark { get; set; }

        [Column("Grade")]
        [StringLength(5)]
        public string? Grade { get; set; }

        [Column("GradePoint")]
        public decimal? GradePoint { get; set; }

        [Column("CreditHours")]
        public int? CreditHours { get; set; }

        // ========== ASSESSMENT BREAKDOWN ==========
        [Column("CourseworkMark")]
        public decimal? CourseworkMark { get; set; }

        [Column("CourseworkWeight")]
        public int CourseworkWeight { get; set; } = 20;

        [Column("TestMark")]
        public decimal? TestMark { get; set; }

        [Column("TestWeight")]
        public int TestWeight { get; set; } = 20;

        [Column("FinalExamMark")]
        public decimal? FinalExamMark { get; set; }

        [Column("FinalExamWeight")]
        public int FinalExamWeight { get; set; } = 60;

        [Column("CalculatedMark")]
        public decimal? CalculatedMark { get; set; }

        // ========== SUPPLEMENTARY EXAM ==========
        public bool IsSupplementaryEligible { get; set; } = false;
        public bool SupplementaryTaken { get; set; } = false;
        public decimal? SupplementaryMark { get; set; }
        public string? SupplementaryGrade { get; set; }
        public decimal? SupplementaryGradePoint { get; set; }

        // ========== APPROVAL & STATUS ==========
        [Column("Status")]
        [StringLength(20)]
        public string? Status { get; set; } = "Pending";

        public DateTime? DateUploaded { get; set; }
        public int? UploadedByAdminId { get; set; }
        public int? ApprovedByAdminId { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UploadedByLecturerId { get; set; }  

        public int? ApprovedByDeanId { get; set; }
        public DateTime? DeanApprovalDate { get; set; }
        public int? SignedOffByRegistrarId { get; set; }
        public DateTime? RegistrarSignOffDate { get; set; }

        public virtual Student? Student { get; set; }
        public virtual Course? Course { get; set; }

       
        public decimal? CalculateFinalMark()
        {
            if (CourseworkMark == null || TestMark == null || FinalExamMark == null)
                return null;

            decimal courseworkContribution = (CourseworkMark.Value * CourseworkWeight) / 100m;
            decimal testContribution = (TestMark.Value * TestWeight) / 100m;
            decimal examContribution = (FinalExamMark.Value * FinalExamWeight) / 100m;

            return Math.Round(courseworkContribution + testContribution + examContribution, 2);
        }

        
        public bool ValidateWeights()
        {
            return (CourseworkWeight + TestWeight + FinalExamWeight) == 100;
        }

       
        public bool CheckSupplementaryEligibility()
        {
            return (Grade == "F" || (Mark.HasValue && Mark < 50));
        }
    }
}