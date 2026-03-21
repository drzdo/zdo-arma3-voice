namespace ArmaVoice.Server.Audio;

/// <summary>
/// Applies radio communication effects to audio:
/// band-pass filter (300Hz-3kHz), soft clipping, white noise, and squelch sounds.
/// </summary>
public class RadioEffect
{
    /// <summary>
    /// Apply radio effects to mono audio samples.
    /// Returns mono float array with radio-style processing applied.
    /// </summary>
    public float[] ApplyRadioEffect(float[] monoSamples, int sampleRate)
    {
        // 1. Band-pass filter 300Hz - 3kHz using cascaded high-pass + low-pass
        var bandPassed = ApplyBandPassFilter(monoSamples, sampleRate, 300f, 3000f);

        // 2. Soft clipping/distortion using tanh
        for (int i = 0; i < bandPassed.Length; i++)
        {
            // Drive the signal a bit harder for more distortion character
            bandPassed[i] = MathF.Tanh(bandPassed[i] * 2f);
        }

        // 3. Mix in low-level white noise
        var rng = new Random(42); // Deterministic seed for reproducibility
        for (int i = 0; i < bandPassed.Length; i++)
        {
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.02f;
            bandPassed[i] += noise;
        }

        // 4. Prepend squelch sound (~50ms burst of noise fading in)
        int squelchSamples = (int)(sampleRate * 0.05f); // 50ms
        var squelchIn = GenerateSquelch(squelchSamples, rng, fadeIn: true);

        // 5. Append squelch sound (~50ms burst of noise fading out)
        var squelchOut = GenerateSquelch(squelchSamples, rng, fadeIn: false);

        // Combine: squelch-in + processed audio + squelch-out
        var result = new float[squelchIn.Length + bandPassed.Length + squelchOut.Length];
        squelchIn.CopyTo(result, 0);
        bandPassed.CopyTo(result, squelchIn.Length);
        squelchOut.CopyTo(result, squelchIn.Length + bandPassed.Length);

        return result;
    }

    /// <summary>
    /// Apply a band-pass filter using cascaded single-pole IIR high-pass and low-pass filters.
    /// </summary>
    private static float[] ApplyBandPassFilter(float[] samples, int sampleRate, float lowCutHz, float highCutHz)
    {
        var output = new float[samples.Length];

        // High-pass filter: removes frequencies below lowCutHz
        // alpha_hp = RC / (RC + dt), where RC = 1 / (2*PI*fc), dt = 1/sampleRate
        float rcHp = 1f / (2f * MathF.PI * lowCutHz);
        float dtHp = 1f / sampleRate;
        float alphaHp = rcHp / (rcHp + dtHp);

        float prevInputHp = 0f;
        float prevOutputHp = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            // y[n] = alpha * (y[n-1] + x[n] - x[n-1])
            output[i] = alphaHp * (prevOutputHp + samples[i] - prevInputHp);
            prevInputHp = samples[i];
            prevOutputHp = output[i];
        }

        // Low-pass filter: removes frequencies above highCutHz
        // alpha_lp = dt / (RC + dt), where RC = 1 / (2*PI*fc)
        float rcLp = 1f / (2f * MathF.PI * highCutHz);
        float dtLp = 1f / sampleRate;
        float alphaLp = dtLp / (rcLp + dtLp);

        float prevOutputLp = 0f;

        for (int i = 0; i < output.Length; i++)
        {
            // y[n] = alpha * x[n] + (1 - alpha) * y[n-1]
            output[i] = alphaLp * output[i] + (1f - alphaLp) * prevOutputLp;
            prevOutputLp = output[i];
        }

        return output;
    }

    /// <summary>
    /// Generate a squelch burst: band-passed noise with a fade envelope.
    /// </summary>
    private static float[] GenerateSquelch(int sampleCount, Random rng, bool fadeIn)
    {
        var squelch = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            // White noise
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // Envelope: linear fade
            float envelope = fadeIn
                ? (float)i / sampleCount
                : 1f - (float)i / sampleCount;

            squelch[i] = noise * envelope * 0.3f;
        }

        return squelch;
    }
}
