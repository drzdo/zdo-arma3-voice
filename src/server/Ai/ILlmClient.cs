namespace ZdoArmaVoice.Server.Ai;

public interface ILlmClient
{
    /// <summary>
    /// Send a completion request with a system prompt and messages.
    /// Returns the assistant's text response.
    /// </summary>
    Task<string?> CompleteAsync(string systemPrompt, List<LlmMessage> messages, float temperature = 0.1f, int maxTokens = 300);
}

/// <summary>
/// A single message in a conversation. Content is text; optionally attach one image.
/// Images are not persisted in history — they are for the current request only.
/// </summary>
public record LlmMessage(string Role, string Content, LlmImage? Image = null);

/// <summary>
/// An image attachment. Base64-encoded, with MIME type (e.g. "image/jpeg", "image/png").
/// </summary>
public record LlmImage(string Base64Data, string MediaType);
