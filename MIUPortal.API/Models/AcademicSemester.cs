using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("AcademicSemesters")]
    public class AcademicSemester
    {
        [Key]
        public int SemesterId { get; set; }

        public string AcademicYear { get; set; } = string.Empty;

        public int Semester { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; }
    }
}