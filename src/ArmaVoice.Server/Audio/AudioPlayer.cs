using NAudio.Wave;

namespace ArmaVoice.Server.Audio;

/// <summary>
/// Plays audio through speakers using NAudio.
/// Supports both raw WAV byte arrays and float PCM sample arrays.
/// </summary>
public class AudioPlayer : IDisposable
{
    private WaveOutEvent? _waveOut;

    public AudioPlayer()
    {
        _waveOut = new WaveOutEvent();
    }

    /// <summary>
    /// Play raw WAV bytes through the default output device.
    /// </summary>
    public void PlayWav(byte[] wavData)
    {
        if (wavData.Length == 0 || _waveOut == null)
            return;

        Stop();

        var stream = new MemoryStream(wavData);
        var reader = new WaveFileReader(stream);

        _waveOut = new WaveOutEvent();
        _waveOut.Init(reader);
        _waveOut.Play();
    }

    /// <summary>
    /// Play float PCM samples through the default output device.
    /// Samples are expected as interleaved float32 values in [-1, 1].
    /// For mono, provide channelCount=1. For stereo (interleaved L,R,L,R), provide channelCount=2.
    /// </summary>
    public void PlaySamples(float[] samples, int sampleRate = 22050)
    {
        PlaySamples(samples, sampleRate, channelCount: 1);
    }

    /// <summary>
    /// Play float PCM samples with a specified channel count.
    /// </summary>
    public void PlaySamples(float[] samples, int sampleRate, int channelCount)
    {
        if (samples.Length == 0 || _waveOut == null)
            return;

        Stop();

        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount);
        var buffer = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, buffer, 0, buffer.Length);

        var provider = new RawSourceWaveStream(new MemoryStream(buffer), format);

        _waveOut = new WaveOutEvent();
        _waveOut.Init(provider);
        _waveOut.Play();
    }

    /// <summary>
    /// True if audio is currently playing.
    /// </summary>
    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    /// <summary>
    /// Stop any currently playing audio.
    /// </summary>
    public void Stop()
    {
        if (_waveOut?.PlaybackState == PlaybackState.Playing)
        {
            _waveOut.Stop();
        }
    }

    public void Dispose()
    {
        Stop();
        _waveOut?.Dispose();
        _waveOut = null;
    }
}
