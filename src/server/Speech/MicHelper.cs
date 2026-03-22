using NAudio.Wave;

namespace ZdoArmaVoice.Server.Speech;

public static class MicHelper
{
    public static void ListDevices()
    {
        var count = WaveInEvent.DeviceCount;
        Log.Info("Mic", $"Audio input devices ({count}):");
        for (int i = 0; i < count; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            Log.Info("Mic", $"  [{i}] {caps.ProductName} (channels: {caps.Channels})");
        }
        if (count == 0)
            Log.Warn("Mic", "No audio input devices found!");
    }

    public static WaveInEvent CreateWaveIn(int deviceIndex = -1)
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
            Log.Info("Mic", $"Using device [{deviceIndex}]: {caps.ProductName}");
        }
        else
        {
            Log.Info("Mic", "Using default audio device");
        }

        return waveIn;
    }
}
