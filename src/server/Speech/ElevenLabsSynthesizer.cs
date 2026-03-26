using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace ZdoArmaVoice.Server.Speech;

/// <summary>
/// Text-to-speech via ElevenLabs API.
/// Resolves voice ID from context: unit name → side → default.
/// </summary>
public class ElevenLabsSynthesizer : ISpeechSynthesizer
{
    private readonly HttpClient _http;
    private readonly string _modelId;
    private readonly float _stability;
    private readonly float _similarityBoost;
    private readonly float _style;
    private readonly bool _useSpeakerBoost;
    private readonly Dictionary<string, List<string>> _voices;
    private readonly Dictionary<string, string> _unitVoiceAssignments = new();
    private readonly Random _rng = new();

    public ElevenLabsSynthesizer(string apiKey, string modelId,
        float stability, float similarityBoost, float style, bool useSpeakerBoost,
        Dictionary<string, List<string>> voices)
    {
        _modelId = modelId;
        _stability = stability;
        _similarityBoost = similarityBoost;
        _style = style;
        _useSpeakerBoost = useSpeakerBoost;
        _voices = new Dictionary<string, List<string>>(voices, StringComparer.OrdinalIgnoreCase);

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
            Log.Warn("ElevenLabs", "No voice ID resolved. Check tts.elevenlabs.voices config.");
            return [];
        }

        var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";

        var requestBody = new JsonObject
        {
            ["text"] = text,
            ["model_id"] = _modelId,
            ["voice_settings"] = new JsonObject
            {
                ["stability"] = _stability,
                ["similarity_boost"] = _similarityBoost,
                ["style"] = _style,
                ["use_speaker_boost"] = _useSpeakerBoost
            }
        };

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsByteArrayAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorText = Encoding.UTF8.GetString(body[..Math.Min(200, body.Length)]);
                Log.Error("ElevenLabs", $"API error ({response.StatusCode}): {errorText}");
                return [];
            }

            Log.Info("ElevenLabs", $"Synthesized {body.Length} bytes (voice={voiceId[..8]}...) for: \"{text[..Math.Min(50, text.Length)]}\"");
            return body;
        }
        catch (Exception ex)
        {
            Log.Error("ElevenLabs", $"Error: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Resolve voice ID: check sticky assignment first, then unit name → side → default.
    /// First call for a unit picks a random voice from the matched pool and caches it.
    /// </summary>
    private string? ResolveVoiceId(SpeechContext? context)
    {
        var unitKey = context?.UnitName ?? "";

        // Return cached assignment if exists
        if (!string.IsNullOrEmpty(unitKey) && _unitVoiceAssignments.TryGetValue(unitKey, out var cached))
            return cached;

        // Find the voice pool
        List<string>? pool = null;

        // 1. Unit name match — exact first, then contains
        if (!string.IsNullOrEmpty(unitKey))
        {
            if (_voices.TryGetValue(unitKey, out var byExact))
                pool = byExact;

            if (pool == null)
            {
                foreach (var (key, voices) in _voices)
                {
                    if (key is "default" or "blufor" or "opfor" or "indfor" or "civilian") continue;
                    if (unitKey.Contains(key, StringComparison.OrdinalIgnoreCase))
                    { pool = voices; break; }
                }
            }
        }

        // 2. Side match
        if (pool == null && context?.Side != null)
        {
            var sideKey = context.Side.ToUpperInvariant() switch
            {
                "WEST" => "blufor",
                "EAST" => "opfor",
                "GUER" or "RESISTANCE" => "indfor",
                "CIV" or "CIVILIAN" => "civilian",
                _ => context.Side
            };

            if (_voices.TryGetValue(sideKey, out var bySide))
                pool = bySide;
        }

        // 3. Default
        if (pool == null)
            _voices.TryGetValue("default", out pool);

        if (pool == null || pool.Count == 0)
            return null;

        // Pick random from pool and cache for this unit
        var voiceId = pool[_rng.Next(pool.Count)];
        if (!string.IsNullOrEmpty(unitKey))
        {
            _unitVoiceAssignments[unitKey] = voiceId;
            Log.Info("ElevenLabs", $"Assigned voice {voiceId[..Math.Min(8, voiceId.Length)]}... to {unitKey}");
        }
        return voiceId;
    }
}
