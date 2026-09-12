using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace MIUPortal.API.Services
{
    public class OpenRouterService : IOpenRouterService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        private const string DefaultSystemPrompt =
@"You are MIU Smart Assistant.
You are the official AI assistant for Metropolitan International University.
Help students with:
Admissions
Course Registration
Fees
Results
Timetables
Portal Usage
Academic Information
Be professional.
Keep answers concise.
Never invent university policies.
If unsure, advise students to contact the relevant university office.";

        public OpenRouterService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        // Backward-compatible overload - uses the default system prompt above.
        public Task<string> SendMessageAsync(string message)
        {
            return SendMessageAsync(DefaultSystemPrompt, message);
        }

        // Preferred overload - caller controls the full system prompt, sent as
        // a genuine "system" role message, kept structurally separate from the
        // user's message.
        public async Task<string> SendMessageAsync(string systemPrompt, string userMessage)
        {
            var apiKey = _configuration["OpenRouter:ApiKey"];
            var url = _configuration["OpenRouter:BaseUrl"];
            var models = _configuration
                .GetSection("OpenRouter:Models")
                .Get<string[]>() ?? Array.Empty<string>();

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _configuration["OpenRouter:SiteUrl"]);
            _httpClient.DefaultRequestHeaders.Add("X-Title", _configuration["OpenRouter:SiteName"]);

            foreach (var model in models)
            {
                const int maxAttemptsPerModel = 3; // useful for auto-router entries like "openrouter/free" which land on a different backend each call

                for (int attempt = 1; attempt <= maxAttemptsPerModel; attempt++)
                {
                    try
                    {
                        Console.WriteLine($"Trying model: {model} (attempt {attempt}/{maxAttemptsPerModel})");

                        var request = new
                        {
                            model = model,
                            stream = false,
                            messages = new[]
                            {
                                new { role = "system", content = systemPrompt },
                                new { role = "user", content = userMessage }
                            }
                        };

                        var json = JsonSerializer.Serialize(request);
                        var response = await _httpClient.PostAsync(
                            url,
                            new StringContent(json, Encoding.UTF8, "application/json"));

                        if (response.IsSuccessStatusCode)
                        {
                            var responseJson = await response.Content.ReadAsStringAsync();
                            using JsonDocument document = JsonDocument.Parse(responseJson);
                            var text = document
                                .RootElement
                                .GetProperty("choices")[0]
                                .GetProperty("message")
                                .GetProperty("content")
                                .GetString() ?? "";

                            if (LooksLikeGarbageOutput(text))
                            {
                                Console.WriteLine($"{model} returned non-answer output, retrying: \"{text}\"");
                                continue; // try again - auto-router may pick a different backend
                            }

                            Console.WriteLine($"Success using {model}");
                            return text;
                        }

                        var error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"{model} failed");
                        Console.WriteLine(error);

                        if ((int)response.StatusCode == 429)
                        {
                            await Task.Delay(2000);
                            continue;
                        }

                        break; // non-429 failure - move to the next model entry, retrying this one won't help
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            return "Sorry, all available AI models are currently busy. Please try again in a few moments.";
        }

        /// <summary>
        /// Catches cases where a "free" routed model returns classifier/moderation
        /// metadata (e.g. "User Safety: safe") instead of an actual chat reply.
        /// Deliberately narrow - only rejects clearly non-answer patterns, so it
        /// won't accidentally reject a short-but-valid real answer.
        /// </summary>
        private static bool LooksLikeGarbageOutput(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;

            var trimmed = text.Trim();

            // Known moderation/classifier-style prefixes seen from some free models.
            string[] badPrefixes = { "user safety", "safety:", "classification:", "moderation:", "flag:", "label:" };
            foreach (var prefix in badPrefixes)
            {
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // A real answer to almost any question is going to be more than a
            // couple of words - this catches other terse metadata-like outputs
            // without being so strict it rejects genuinely short valid replies.
            if (trimmed.Length < 8 && !trimmed.Contains(' '))
                return true;

            return false;
        }
    }
}