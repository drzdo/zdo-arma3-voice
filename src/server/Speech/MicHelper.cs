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
        set { }
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

    private int _dataCallbackCount;
    private int _silentCallbackCount;
    private DateTime _lastDiagLog = DateTime.MinValue;

    private void OnCaptureData(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        _dataCallbackCount++;

        // Check if buffer is all silence (zeroes)
        bool isSilent = true;
        for (int i = 0; i < Math.Min(e.BytesRecorded, 200); i++)
        {
            if (e.Buffer[i] != 0) { isSilent = false; break; }
        }
        if (isSilent) _silentCallbackCount++;

        // Log diagnostics every 5 seconds
        if ((DateTime.UtcNow - _lastDiagLog).TotalSeconds >= 5)
        {
            _lastDiagLog = DateTime.UtcNow;
            var pctSilent = _dataCallbackCount > 0 ? _silentCallbackCount * 100 / _dataCallbackCount : 0;
            Log.Info("Mic", $"Diag: {_dataCallbackCount} callbacks, {_silentCallbackCount} silent ({pctSilent}%), last chunk {e.BytesRecorded}B, format={_capture.WaveFormat.SampleRate}Hz/{_capture.WaveFormat.BitsPerSample}bit/{_capture.WaveFormat.Channels}ch");
            _dataCallbackCount = 0;
            _silentCallbackCount = 0;
        }

        // Detect if device format changed (e.g. OBS started and changed shared mode format)
        var currentFormat = _capture.WaveFormat;
        if (currentFormat.SampleRate != _sourceFormat.SampleRate
            || currentFormat.BitsPerSample != _sourceFormat.BitsPerSample
            || currentFormat.Channels != _sourceFormat.Channels)
        {
            Log.Warn("Mic", $"Device format CHANGED: {_sourceFormat.SampleRate}Hz/{_sourceFormat.BitsPerSample}bit/{_sourceFormat.Channels}ch -> {currentFormat.SampleRate}Hz/{currentFormat.BitsPerSample}bit/{currentFormat.Channels}ch");
        }

        // Use the capture's current format, not the cached one, to handle format changes
        var liveFormat = _capture.WaveFormat;
        var sourceProvider = new RawSourceWaveStream(new MemoryStream(e.Buffer, 0, e.BytesRecorded), liveFormat);
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
    /// <summary>
    /// mic_mode: "wasapi" (shared, default) or "mme" (legacy, works with OBS)
    /// </summary>
    public static void ListDevices(string mode = "wasapi")
    {
        if (mode == "mme")
        {
            var count = WaveInEvent.DeviceCount;
            Log.Info("Mic", $"MME audio input devices ({count}):");
            for (int i = 0; i < count; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                Log.Info("Mic", $"  [{i}] {caps.ProductName} (channels: {caps.Channels})");
            }
            if (count == 0)
                Log.Warn("Mic", "No MME audio input devices found!");
        }
        else
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            Log.Info("Mic", $"WASAPI audio input devices ({devices.Count}):");
            for (int i = 0; i < devices.Count; i++)
            {
                Log.Info("Mic", $"  [{i}] {devices[i].FriendlyName}");
            }
            if (devices.Count == 0)
                Log.Warn("Mic", "No WASAPI audio input devices found!");
        }
    }

    public static IWaveIn CreateWaveIn(int deviceIndex = -1, string mode = "wasapi")
    {
        if (mode == "mme")
            return CreateMme(deviceIndex);
        return CreateWasapi(deviceIndex);
    }

    private static IWaveIn CreateMme(int deviceIndex)
    {
        var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50
        };

        if (deviceIndex >= 0 && deviceIndex < WaveInEvent.DeviceCount)
        {
            waveIn.DeviceNumber = deviceIndex;
            var caps = WaveInEvent.GetCapabilities(deviceIndex);
            Log.Info("Mic", $"MME device [{deviceIndex}]: {caps.ProductName}");
        }
        else
        {
            Log.Info("Mic", "MME default device");
        }

        return waveIn;
    }

    private static IWaveIn CreateWasapi(int deviceIndex)
    {
        var enumerator = new MMDeviceEnumerator();
        MMDevice device;

        if (deviceIndex >= 0)
        {
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            if (deviceIndex < devices.Count)
            {
                device = devices[deviceIndex];
                Log.Info("Mic", $"WASAPI device [{deviceIndex}]: {device.FriendlyName}");
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
            Log.Info("Mic", $"WASAPI default device: {device.FriendlyName}");
        }

        return new ResamplingWaveIn(device);
    }
}
