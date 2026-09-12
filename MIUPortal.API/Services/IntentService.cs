namespace MIUPortal.API.Services
{
    public interface IIntentService
    {
        string DetectIntent(string message);
    }
}

namespace MIUPortal.API.Services
{
    public class IntentService : IIntentService
    {
        public string DetectIntent(string message)
        {
            message = message.ToLower();

            if (message.Contains("how many students") ||
                message.Contains("student count") ||
                message.Contains("registered students"))
                return "STUDENT_COUNT";

            if (message.Contains("how many courses") ||
                message.Contains("course count"))
                return "COURSE_COUNT";

            if (message.Contains("gpa") ||
                message.Contains("cgpa"))
                return "GPA";

            if (message.Contains("fees") ||
                message.Contains("balance"))
                return "BALANCE";

            if (message.Contains("course"))
                return "COURSES";

            if (message.Contains("result"))
                return "RESULTS";

            if (message.Contains("document"))
                return "DOCUMENTS";

            if (message.Contains("timetable"))
                return "TIMETABLE";

            if (message.Contains("registration"))
                return "REGISTRATION";

            return "AI";
        }
    }
}