using System.Threading.Tasks;

namespace MIUPortal.API.Services
{
    public interface IMIUChatbotService
    {
        Task<string> ProcessMessageAsync(string message, int? studentId = null);
    }
}