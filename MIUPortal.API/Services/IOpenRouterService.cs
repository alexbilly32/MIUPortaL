using System.Threading.Tasks;

namespace MIUPortal.API.Services
{
    public interface IOpenRouterService
    {
        // Original signature - kept for backward compatibility with any other
        // callers. Uses the default MIU Smart Assistant system prompt baked
        // into the implementation.
        Task<string> SendMessageAsync(string message);

        // New overload: lets callers (like MIUChatbotService) supply their own
        // full system prompt, sent as a genuine "system" role message rather
        // than mixed into user content. Use this for anything that needs
        // stricter guardrails than the default prompt provides.
        Task<string> SendMessageAsync(string systemPrompt, string userMessage);
    }
}