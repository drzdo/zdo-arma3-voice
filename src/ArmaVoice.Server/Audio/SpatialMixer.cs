namespace ArmaVoice.Server.Audio;

/// <summary>
/// Applies spatial audio effects based on listener and source positions.
/// Produces interleaved stereo output with panning, distance attenuation,
/// and distance-based low-pass filtering.
/// </summary>
public class SpatialMixer
{
    /// <summary>
    /// Apply spatial audio processing to mono input samples.
    /// Returns interleaved stereo samples [L, R, L, R, ...].
    /// </summary>
    /// <param name="monoSamples">Input mono audio samples.</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <param name="listenerPos">Listener position [x, y, z].</param>
    /// <param name="listenerDir">Listener facing direction in degrees (0 = north, clockwise).</param>
    /// <param name="sourcePos">Audio source position [x, y, z].</param>
    /// <returns>Interleaved stereo float array.</returns>
    public float[] ApplySpatialAudio(
        float[] monoSamples,
        int sampleRate,
        float[] listenerPos,
        float listenerDir,
        float[] sourcePos)
    {
        // 1. Compute bearing from listener to source relative to listener direction
        float dx = sourcePos[0] - listenerPos[0];
        float dy = sourcePos[1] - listenerPos[1];

        // atan2(dx, dy) gives angle from north (Y+) clockwise, in radians
        float absoluteBearingRad = MathF.Atan2(dx, dy);
        float absoluteBearingDeg = absoluteBearingRad * (180f / MathF.PI);

        // Relative bearing: subtract listener direction, normalize to [-180, 180]
        float relativeBearingDeg = absoluteBearingDeg - listenerDir;
        relativeBearingDeg = NormalizeDegrees(relativeBearingDeg);

        float relativeBearingRad = relativeBearingDeg * (MathF.PI / 180f);

        // 2. Pan: sin(bearing_radians) gives -1 (left) to 1 (right)
        float pan = MathF.Sin(relativeBearingRad);

        // Equal-power panning: pan_angle = (pan + 1) * PI/4
        // At pan=-1: angle=0 → left=cos(0)=1, right=sin(0)=0
        // At pan= 0: angle=PI/4 → left=cos(PI/4)=0.707, right=sin(PI/4)=0.707
        // At pan=+1: angle=PI/2 → left=cos(PI/2)=0, right=sin(PI/2)=1
        float panAngle = (pan + 1f) * MathF.PI / 4f;
        float leftGain = MathF.Cos(panAngle);
        float rightGain = MathF.Sin(panAngle);

        // 3. Distance attenuation: 1.0 / max(1.0, distance / 5.0)
        // Within 5m is full volume, then falls off
        float distance = MathF.Sqrt(dx * dx + dy * dy +
            (sourcePos.Length > 2 && listenerPos.Length > 2
                ? (sourcePos[2] - listenerPos[2]) * (sourcePos[2] - listenerPos[2])
                : 0f));

        float distanceAttenuation = 1f / MathF.Max(1f, distance / 5f);

        // 4. Low-pass filter: single-pole IIR with alpha = clamp(1.0 - distance/50.0, 0.1, 1.0)
        // alpha=1 means no filtering, alpha close to 0 means heavy filtering (muffled)
        float alpha = Math.Clamp(1f - distance / 50f, 0.1f, 1f);

        // Apply low-pass filter to mono samples first
        var filtered = new float[monoSamples.Length];
        float prevSample = 0f;

        for (int i = 0; i < monoSamples.Length; i++)
        {
            // Single-pole IIR low-pass: y[n] = alpha * x[n] + (1 - alpha) * y[n-1]
            filtered[i] = alpha * monoSamples[i] + (1f - alpha) * prevSample;
            prevSample = filtered[i];
        }

        // Build interleaved stereo output with panning and attenuation
        var stereo = new float[monoSamples.Length * 2];

        for (int i = 0; i < filtered.Length; i++)
        {
            float sample = filtered[i] * distanceAttenuation;
            stereo[i * 2] = sample * leftGain;
            stereo[i * 2 + 1] = sample * rightGain;
        }

        return stereo;
    }

    /// <summary>
    /// Normalize angle in degrees to the range [-180, 180].
    /// </summary>
    private static float NormalizeDegrees(float degrees)
    {
        while (degrees > 180f) degrees -= 360f;
        while (degrees < -180f) degrees += 360f;
        return degrees;
    }
}
