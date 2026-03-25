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
    private readonly float[] _cleanSamples;
    private readonly float[]? _radioSamples;
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

    /// <param name="cleanSamples">Clean (non-effected) mono samples for spatial audio.</param>
    /// <param name="radioSamples">Radio-effected mono samples (null if not radio mode).</param>
    /// <param name="isRadio">If true, play both radio and spatial signals.</param>
    /// <param name="radioPan">Radio pan: -1=left, 0=center, 1=right.</param>
    /// <param name="radioVolume">Radio volume: 0=silent, 1=full.</param>
    public SpatialSampleProvider(
        float[] cleanSamples,
        float[]? radioSamples,
        int sampleRate,
        GameState gameState,
        string npcNetId,
        UnitRegistry unitRegistry,
        bool isRadio = false,
        float radioPan = 0f,
        float radioVolume = 1f)
    {
        _cleanSamples = cleanSamples;
        _radioSamples = radioSamples;
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
        int monoAvailable = _cleanSamples.Length - _position;
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

        // Spatial params (shared)
        float attenuation = 1f / MathF.Max(1f, distance / 5f);
        float alpha = Math.Clamp(1f - distance / 50f, 0.1f, 1f);

        for (int i = 0; i < framesToProcess; i++)
        {
            float clean = _cleanSamples[_position + i];

            // Spatial component (always computed from clean audio)
            float spatialMono = clean * attenuation;
            float filteredL = alpha * (spatialMono * leftGain) + (1f - alpha) * _prevFilteredL;
            float filteredR = alpha * (spatialMono * rightGain) + (1f - alpha) * _prevFilteredR;
            _prevFilteredL = filteredL;
            _prevFilteredR = filteredR;

            if (_isRadio && _radioSamples != null)
            {
                // Radio + spatial: radio-effected signal with radio pan, plus spatial
                float radio = _radioSamples[_position + i];
                float panAngleR = (_radioPan + 1f) * MathF.PI / 4f;
                float radioL = radio * _radioVolume * MathF.Cos(panAngleR);
                float radioR = radio * _radioVolume * MathF.Sin(panAngleR);

                buffer[offset + i * 2] = radioL + filteredL;
                buffer[offset + i * 2 + 1] = radioR + filteredR;
            }
            else
            {
                // Direct: spatial only
                buffer[offset + i * 2] = filteredL;
                buffer[offset + i * 2 + 1] = filteredR;
            }
        }

        _position += framesToProcess;
        return framesToProcess * 2;
    }
}
