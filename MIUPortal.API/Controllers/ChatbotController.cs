using Microsoft.AspNetCore.Mvc;
using MIUPortal.API.Services;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/chatbot")]
    public class ChatbotController : ControllerBase
    {
        private readonly IMIUChatbotService _chatbot;

        public ChatbotController(IMIUChatbotService chatbot)
        {
            _chatbot = chatbot;
        }

        public class ChatRequest
        {
            public string Message { get; set; } = "";
        }

        [HttpPost]
        public async Task<IActionResult> Chat(ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest();

            int? studentId = null;

            var studentClaim = User.FindFirst("studentId")?.Value;
            if (!string.IsNullOrEmpty(studentClaim) && int.TryParse(studentClaim, out var parsedId))
            {
                studentId = parsedId;
            }

            var reply = await _chatbot.ProcessMessageAsync(request.Message, studentId);

            return Ok(new
            {
                success = true,
                reply
            });
        }
    }
}