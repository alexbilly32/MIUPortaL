public class TranscriptDto
{
    public string RegNumber { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Programme { get; set; } = "";

    public decimal CGPA { get; set; }

    public string DegreeClass { get; set; } = "";

    public int TotalCredits { get; set; }

    public DateTime? GraduationDate { get; set; }

    public List<TranscriptCourseDto> Courses { get; set; }
        = new();
}

public class TranscriptCourseDto
{
    public int SemesterId { get; set; }

    public string CourseCode { get; set; } = "";

    public string CourseName { get; set; } = "";

    public int CreditHours { get; set; }

    public decimal Mark { get; set; }

    public string Grade { get; set; } = "";

    public decimal GradePoint { get; set; }
}