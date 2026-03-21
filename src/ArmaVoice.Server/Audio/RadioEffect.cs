namespace ArmaVoice.Server.Audio;

public class RadioEffect
{
    private readonly float _lowCutHz;
    private readonly float _highCutHz;
    private readonly float _distortion;
    private readonly float _noiseLevel;
    private readonly float _squelchDuration;
    private readonly bool _useBiquad;

    public RadioEffect(float lowCutHz = 300f, float highCutHz = 3000f, float distortion = 2f,
        float noiseLevel = 0.02f, float squelchDuration = 0.05f, bool useBiquad = true)
    {
        _lowCutHz = lowCutHz;
        _highCutHz = highCutHz;
        _distortion = distortion;
        _noiseLevel = noiseLevel;
        _squelchDuration = squelchDuration;
        _useBiquad = useBiquad;
    }

    public float[] ApplyRadioEffect(float[] monoSamples, int sampleRate)
    {
        var bandPassed = _useBiquad
            ? ApplyBiquadBandPass(monoSamples, sampleRate, _lowCutHz, _highCutHz)
            : ApplySimpleBandPass(monoSamples, sampleRate, _lowCutHz, _highCutHz);

        for (int i = 0; i < bandPassed.Length; i++)
            bandPassed[i] = MathF.Tanh(bandPassed[i] * _distortion);

        var rng = new Random(42);
        for (int i = 0; i < bandPassed.Length; i++)
            bandPassed[i] += (float)(rng.NextDouble() * 2.0 - 1.0) * _noiseLevel;

        int squelchSamples = (int)(sampleRate * _squelchDuration);
        var squelchIn = GenerateSquelch(squelchSamples, rng, fadeIn: true);
        var squelchOut = GenerateSquelch(squelchSamples, rng, fadeIn: false);

        var result = new float[squelchIn.Length + bandPassed.Length + squelchOut.Length];
        squelchIn.CopyTo(result, 0);
        bandPassed.CopyTo(result, squelchIn.Length);
        squelchOut.CopyTo(result, squelchIn.Length + bandPassed.Length);

        return result;
    }

    /// <summary>
    /// Biquad band-pass filter — proper 2nd-order IIR with steep rolloff and flat passband.
    /// Two cascaded biquads: high-pass at lowCut + low-pass at highCut.
    /// </summary>
    private static float[] ApplyBiquadBandPass(float[] samples, int sampleRate, float lowCutHz, float highCutHz)
    {
        var output = new float[samples.Length];
        Array.Copy(samples, output, samples.Length);

        ApplyBiquadHighPass(output, sampleRate, lowCutHz);
        ApplyBiquadLowPass(output, sampleRate, highCutHz);

        return output;
    }

    private static void ApplyBiquadHighPass(float[] samples, int sampleRate, float cutoffHz)
    {
        float w0 = 2f * MathF.PI * cutoffHz / sampleRate;
        float cos_w0 = MathF.Cos(w0);
        float sin_w0 = MathF.Sin(w0);
        float alpha = sin_w0 / (2f * 0.707f); // Q = 0.707 (Butterworth)

        float b0 = (1f + cos_w0) / 2f;
        float b1 = -(1f + cos_w0);
        float b2 = (1f + cos_w0) / 2f;
        float a0 = 1f + alpha;
        float a1 = -2f * cos_w0;
        float a2 = 1f - alpha;

        ApplyBiquad(samples, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }

    private static void ApplyBiquadLowPass(float[] samples, int sampleRate, float cutoffHz)
    {
        float w0 = 2f * MathF.PI * cutoffHz / sampleRate;
        float cos_w0 = MathF.Cos(w0);
        float sin_w0 = MathF.Sin(w0);
        float alpha = sin_w0 / (2f * 0.707f);

        float b0 = (1f - cos_w0) / 2f;
        float b1 = 1f - cos_w0;
        float b2 = (1f - cos_w0) / 2f;
        float a0 = 1f + alpha;
        float a1 = -2f * cos_w0;
        float a2 = 1f - alpha;

        ApplyBiquad(samples, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }

    private static void ApplyBiquad(float[] samples, float b0, float b1, float b2, float a1, float a2)
    {
        float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float x0 = samples[i];
            float y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x0;
            y2 = y1; y1 = y0;
            samples[i] = y0;
        }
    }

    /// <summary>
    /// Simple single-pole IIR band-pass — basic but fast.
    /// </summary>
    private static float[] ApplySimpleBandPass(float[] samples, int sampleRate, float lowCutHz, float highCutHz)
    {
        var output = new float[samples.Length];

        float rcHp = 1f / (2f * MathF.PI * lowCutHz);
        float dt = 1f / sampleRate;
        float alphaHp = rcHp / (rcHp + dt);

        float prevInputHp = 0f, prevOutputHp = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            output[i] = alphaHp * (prevOutputHp + samples[i] - prevInputHp);
            prevInputHp = samples[i];
            prevOutputHp = output[i];
        }

        float rcLp = 1f / (2f * MathF.PI * highCutHz);
        float alphaLp = dt / (rcLp + dt);

        float prevOutputLp = 0f;
        for (int i = 0; i < output.Length; i++)
        {
            output[i] = alphaLp * output[i] + (1f - alphaLp) * prevOutputLp;
            prevOutputLp = output[i];
        }

        return output;
    }

    private static float[] GenerateSquelch(int sampleCount, Random rng, bool fadeIn)
    {
        var squelch = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float envelope = fadeIn ? (float)i / sampleCount : 1f - (float)i / sampleCount;
            squelch[i] = noise * envelope * 0.3f;
        }
        return squelch;
    }
}
