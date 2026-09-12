using System.ComponentModel.DataAnnotations;
namespace MIUPortal.API.Models
{
    public class Timetable
    {
        [Key]
        public int TimetableId { get; set; }
        public string ProgrammeCode { get; set; } = string.Empty;
        public string? ProgrammeName { get; set; }
        public int Year { get; set; }
        public int Semester { get; set; }
        public int CourseId { get; set; }
        public string? CourseCode { get; set; }
        public string? CourseName { get; set; }
        public string? DayOfWeek { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string? Venue { get; set; }
        public string? Lecturer { get; set; }
        public string? CampusCode { get; set; }
        public DateTime? DateCreated { get; set; }      // ✅ FIXED: NOW NULLABLE
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
    }
}