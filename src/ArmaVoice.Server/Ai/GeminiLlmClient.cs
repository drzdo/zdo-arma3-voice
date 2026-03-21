using System.Text.Json;

namespace ArmaVoice.Server.Ai;

public class GeminiLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiLlmClient(string apiKey, string model = "gemini-2.0-flash")
    {
        _apiKey = apiKey;
        _model = model;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string?> CompleteAsync(string systemPrompt, List<LlmMessage> messages, float temperature = 0.1f, int maxTokens = 300)
    {
        // Build Gemini contents array from messages
        var contents = messages.Select(m => new
        {
            role = m.Role == "assistant" ? "model" : "user",
            parts = new[] { new { text = m.Content } }
        }).ToArray();

        var requestBody = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents,
            generationConfig = new { temperature, maxOutputTokens = maxTokens }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            var response = await _http.PostAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Gemini] API error ({response.StatusCode}): {body[..Math.Min(200, body.Length)]}");
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0) return null;

            return candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString()?.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gemini] Error: {ex.Message}");
            return null;
        }
    }
}
