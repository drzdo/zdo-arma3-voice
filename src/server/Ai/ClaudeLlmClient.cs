using System.Text.Json;
using System.Text.Json.Nodes;

namespace ZdoArmaVoice.Server.Ai;

public class ClaudeLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public ClaudeLlmClient(string apiKey, string model = "claude-sonnet-4-20250514")
    {
        _apiKey = apiKey;
        _model = model;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string?> CompleteAsync(string systemPrompt, List<LlmMessage> messages, float temperature = 0.1f, int maxTokens = 300)
    {
        var msgsArray = new JsonArray();
        foreach (var m in messages)
        {
            if (m.Image != null)
            {
                msgsArray.Add(new JsonObject
                {
                    ["role"] = m.Role,
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "image",
                            ["source"] = new JsonObject
                            {
                                ["type"] = "base64",
                                ["media_type"] = m.Image.MediaType,
                                ["data"] = m.Image.Base64Data
                            }
                        },
                        new JsonObject { ["type"] = "text", ["text"] = m.Content }
                    }
                });
            }
            else
            {
                msgsArray.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content });
            }
        }

        var requestBody = new JsonObject
        {
            ["model"] = _model,
            ["max_tokens"] = maxTokens,
            ["system"] = systemPrompt,
            ["messages"] = msgsArray
        };

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(requestBody.ToJsonString(), System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Claude", $"API error ({response.StatusCode}): {body[..Math.Min(200, body.Length)]}");
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var contentArray = doc.RootElement.GetProperty("content");
            if (contentArray.GetArrayLength() == 0) return null;

            return contentArray[0].GetProperty("text").GetString()?.Trim();
        }
        catch (Exception ex)
        {
            Log.Error("Claude", $"Error: {ex.Message}");
            return null;
        }
    }
}
