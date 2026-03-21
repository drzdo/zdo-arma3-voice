using NAudio.Wave;

namespace ArmaVoice.Server.Audio;

public class AudioPlayer : IDisposable
{
    private WaveOutEvent? _waveOut;

    public AudioPlayer()
    {
        _waveOut = new WaveOutEvent();
    }

    /// <summary>
    /// Play from an ISampleProvider (used for real-time spatial audio).
    /// </summary>
    public void Play(ISampleProvider provider)
    {
        Stop();
        _waveOut = new WaveOutEvent();
        _waveOut.Init(provider);
        _waveOut.Play();
    }

    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    public void Stop()
    {
        if (_waveOut?.PlaybackState == PlaybackState.Playing)
            _waveOut.Stop();
    }

    public void Dispose()
    {
        Stop();
        _waveOut?.Dispose();
        _waveOut = null;
    }
}
