using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ArmaVoice.Server.Speech;

/// <summary>
/// Text-to-speech via ElevenLabs API.
/// Resolves voice ID from context: unit name → side → default.
/// </summary>
public class ElevenLabsSynthesizer : ISpeechSynthesizer
{
    private readonly HttpClient _http;
    private readonly string _modelId;
    private readonly Dictionary<string, string> _voices;

    /// <param name="voices">
    /// Voice mapping. Keys: "default", "blufor", "opfor", "indfor", or a unit name.
    /// Values: ElevenLabs voice IDs.
    /// </param>
    public ElevenLabsSynthesizer(string apiKey, string modelId, Dictionary<string, string> voices)
    {
        _modelId = modelId;
        _voices = new Dictionary<string, string>(voices, StringComparer.OrdinalIgnoreCase);

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("xi-api-key", apiKey);
    }

    public async Task<byte[]> SynthesizeAsync(string text, SpeechContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var voiceId = ResolveVoiceId(context);
        if (string.IsNullOrEmpty(voiceId))
        {
            Console.WriteLine("[ElevenLabsSynthesizer] No voice ID resolved. Check tts.elevenlabs.voices config.");
            return [];
        }

        var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";

        var requestBody = JsonSerializer.Serialize(new
        {
            text,
            model_id = _modelId,
            voice_settings = new
            {
                stability = 0.5,
                similarity_boost = 0.75
            }
        });

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsByteArrayAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorText = Encoding.UTF8.GetString(body[..Math.Min(200, body.Length)]);
                Console.WriteLine($"[ElevenLabsSynthesizer] API error ({response.StatusCode}): {errorText}");
                return [];
            }

            Console.WriteLine($"[ElevenLabsSynthesizer] Synthesized {body.Length} bytes (voice={voiceId[..8]}...) for: \"{text[..Math.Min(50, text.Length)]}\"");
            return body;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ElevenLabsSynthesizer] Error: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Resolve voice ID: unit name → side → default.
    /// </summary>
    private string? ResolveVoiceId(SpeechContext? context)
    {
        // 1. Exact unit name match
        if (context?.UnitName != null && _voices.TryGetValue(context.UnitName, out var byName))
            return byName;

        // 2. Side match (SQF sides: WEST=blufor, EAST=opfor, GUER=indfor, CIV=civilian)
        if (context?.Side != null)
        {
            var sideKey = context.Side.ToUpperInvariant() switch
            {
                "WEST" => "blufor",
                "EAST" => "opfor",
                "GUER" or "RESISTANCE" => "indfor",
                "CIV" or "CIVILIAN" => "civilian",
                _ => context.Side // try raw value too
            };

            if (_voices.TryGetValue(sideKey, out var bySide))
                return bySide;
        }

        // 3. Default
        _voices.TryGetValue("default", out var def);
        return def;
    }
}
