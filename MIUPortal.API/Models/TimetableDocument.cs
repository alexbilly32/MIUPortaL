using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("timetable_documents")]
    public class TimetableDocument
    {
        [Key]
        public int TimetableDocumentId { get; set; }

        public int FacultyId { get; set; }
        public int SemesterId { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int? FileSizeBytes { get; set; }

        public string UploadedByRole { get; set; } = string.Empty; // "Registrar" or "Dean"
        public int UploadedById { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime UploadedAt { get; set; }

        public bool IsActive { get; set; } = true;

        [ForeignKey("FacultyId")]
        public Faculty? Faculty { get; set; }

        [ForeignKey("SemesterId")]
        public AcademicSemester? Semester { get; set; }
    }
}