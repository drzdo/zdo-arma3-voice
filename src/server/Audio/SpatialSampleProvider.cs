using NAudio.Wave;
using ZdoArmaVoice.Server.Game;

namespace ZdoArmaVoice.Server.Audio;

/// <summary>
/// Real-time spatial audio sample provider.
/// Reads mono source samples and outputs stereo with per-chunk spatial positioning
/// based on live GameState (player pos/dir) and NPC position.
/// </summary>
public class SpatialSampleProvider : ISampleProvider
{
    private readonly float[] _monoSamples;
    private readonly GameState _gameState;
    private readonly string _npcNetId;
    private readonly UnitRegistry _unitRegistry;
    private readonly bool _isRadio;
    private readonly float _radioPan;
    private readonly float _radioVolume;
    private int _position;
    private float _prevFilteredL;
    private float _prevFilteredR;

    public WaveFormat WaveFormat { get; }

    /// <param name="isRadio">If true, skip distance attenuation and muffling.</param>
    /// <param name="radioPan">Radio pan: -1=left, 0=center, 1=right.</param>
    /// <param name="radioVolume">Radio volume: 0=silent, 1=full.</param>
    public SpatialSampleProvider(
        float[] monoSamples,
        int sampleRate,
        GameState gameState,
        string npcNetId,
        UnitRegistry unitRegistry,
        bool isRadio = false,
        float radioPan = 0f,
        float radioVolume = 1f)
    {
        _monoSamples = monoSamples;
        _gameState = gameState;
        _npcNetId = npcNetId;
        _unitRegistry = unitRegistry;
        _isRadio = isRadio;
        _radioPan = Math.Clamp(radioPan, -1f, 1f);
        _radioVolume = Math.Clamp(radioVolume, 0f, 1f);
        _position = 0;

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // count is in stereo samples (L,R pairs), so count/2 mono samples needed
        int stereoFrames = count / 2;
        int monoAvailable = _monoSamples.Length - _position;
        int framesToProcess = Math.Min(stereoFrames, monoAvailable);

        if (framesToProcess <= 0)
            return 0;

        // Get current positions from live game state
        var playerPos = _gameState.PlayerPos;
        var playerDir = _gameState.PlayerDir;
        var unit = _unitRegistry.GetUnit(_npcNetId);
        var sourcePos = unit?.Position ?? playerPos;

        // Compute spatial params from current state
        float dx = sourcePos[0] - playerPos[0];
        float dy = sourcePos[1] - playerPos[1];
        float dz = sourcePos.Length > 2 && playerPos.Length > 2
            ? sourcePos[2] - playerPos[2] : 0f;

        float distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        // Bearing
        float absBearingRad = MathF.Atan2(dx, dy);
        float absBearingDeg = absBearingRad * (180f / MathF.PI);
        float relBearingDeg = absBearingDeg - playerDir;
        while (relBearingDeg > 180f) relBearingDeg -= 360f;
        while (relBearingDeg < -180f) relBearingDeg += 360f;
        float relBearingRad = relBearingDeg * (MathF.PI / 180f);

        // Pan (equal-power)
        float pan = MathF.Sin(relBearingRad);
        float panAngle = (pan + 1f) * MathF.PI / 4f;
        float leftGain = MathF.Cos(panAngle);
        float rightGain = MathF.Sin(panAngle);

        for (int i = 0; i < framesToProcess; i++)
        {
            float mono = _monoSamples[_position + i];

            if (_isRadio)
            {
                // Radio: configurable pan + volume
                // pan -1=left, 0=center, 1=right (equal-power)
                float panAngleR = (_radioPan + 1f) * MathF.PI / 4f;
                buffer[offset + i * 2] = mono * _radioVolume * MathF.Cos(panAngleR);
                buffer[offset + i * 2 + 1] = mono * _radioVolume * MathF.Sin(panAngleR);
            }
            else
            {
                // Spatial: distance attenuation + pan + low-pass
                float attenuation = 1f / MathF.Max(1f, distance / 5f);
                float alpha = Math.Clamp(1f - distance / 50f, 0.1f, 1f);

                mono *= attenuation;
                float filteredL = alpha * (mono * leftGain) + (1f - alpha) * _prevFilteredL;
                float filteredR = alpha * (mono * rightGain) + (1f - alpha) * _prevFilteredR;
                _prevFilteredL = filteredL;
                _prevFilteredR = filteredR;

                buffer[offset + i * 2] = filteredL;
                buffer[offset + i * 2 + 1] = filteredR;
            }
        }

        _position += framesToProcess;
        return framesToProcess * 2;
    }
}
