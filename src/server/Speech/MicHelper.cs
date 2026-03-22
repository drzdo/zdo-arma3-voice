using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace ZdoArmaVoice.Server.Speech;

/// <summary>
/// Wraps WasapiCapture in shared mode, resamples to 16kHz/16-bit/mono.
/// </summary>
public class ResamplingWaveIn : IWaveIn, IDisposable
{
    private readonly WasapiCapture _capture;
    private readonly WaveFormat _targetFormat = new(16000, 16, 1);
    private readonly WaveFormat _sourceFormat;

    public WaveFormat WaveFormat
    {
        get => _targetFormat;
        set { /* ignore — always 16kHz/16-bit/mono */ }
    }

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    public ResamplingWaveIn(MMDevice device)
    {
        _capture = new WasapiCapture(device, true, 50);
        _sourceFormat = _capture.WaveFormat;

        Log.Info("Mic", $"Device native format: {_sourceFormat.SampleRate}Hz, {_sourceFormat.BitsPerSample}bit, {_sourceFormat.Channels}ch");

        _capture.DataAvailable += OnCaptureData;
        _capture.RecordingStopped += (s, e) => RecordingStopped?.Invoke(s, e);
    }

    private void OnCaptureData(object? sender, WaveInEventArgs e)
    {
        var sourceProvider = new RawSourceWaveStream(new MemoryStream(e.Buffer, 0, e.BytesRecorded), _sourceFormat);
        var sampleProvider = sourceProvider.ToSampleProvider();

        if (sampleProvider.WaveFormat.Channels > 1)
            sampleProvider = sampleProvider.ToMono();

        ISampleProvider finalProvider = sampleProvider;
        if (sampleProvider.WaveFormat.SampleRate != 16000)
            finalProvider = new WdlResamplingSampleProvider(sampleProvider, 16000);

        var waveProvider = finalProvider.ToWaveProvider16();
        var buffer = new byte[e.BytesRecorded * 2];
        var bytesRead = waveProvider.Read(buffer, 0, buffer.Length);

        if (bytesRead > 0)
            DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, bytesRead));
    }

    public void StartRecording() => _capture.StartRecording();
    public void StopRecording() => _capture.StopRecording();

    public void Dispose()
    {
        _capture.DataAvailable -= OnCaptureData;
        _capture.Dispose();
    }
}

public static class MicHelper
{
    public static void ListDevices()
    {
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        Log.Info("Mic", $"Audio input devices ({devices.Count}):");
        for (int i = 0; i < devices.Count; i++)
        {
            Log.Info("Mic", $"  [{i}] {devices[i].FriendlyName}");
        }
        if (devices.Count == 0)
            Log.Warn("Mic", "No audio input devices found!");
    }

    public static IWaveIn CreateWaveIn(int deviceIndex = -1)
    {
        var enumerator = new MMDeviceEnumerator();
        MMDevice device;

        if (deviceIndex >= 0)
        {
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            if (deviceIndex < devices.Count)
            {
                device = devices[deviceIndex];
                Log.Info("Mic", $"Using device [{deviceIndex}]: {device.FriendlyName}");
            }
            else
            {
                Log.Warn("Mic", $"Device [{deviceIndex}] not found, using default");
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            }
        }
        else
        {
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            Log.Info("Mic", $"Using default device: {device.FriendlyName}");
        }

        return new ResamplingWaveIn(device);
    }
}
