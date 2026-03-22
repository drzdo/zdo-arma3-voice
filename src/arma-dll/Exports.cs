using System.Runtime.InteropServices;
using System.Text.Json;

namespace ZdoArmaVoice.Extension;

public static class Exports
{
    private static readonly CommandQueue InboundQueue = new();
    private static readonly TcpClient Client = new(InboundQueue);

    [UnmanagedCallersOnly(EntryPoint = "RVExtensionVersion")]
    public static void RVExtensionVersion(nint output, int outputSize)
    {
        TcpClient.Log("RVExtensionVersion called");
        WriteOutput(output, outputSize, "1.0.0");
    }

    [UnmanagedCallersOnly(EntryPoint = "RVExtension")]
    public static void RVExtension(nint output, int outputSize, nint function)
    {
        var input = ReadString(function);

        // Simple commands (non-JSON)
        switch (input)
        {
            case "status":
                WriteOutput(output, outputSize, Client.IsConnected ? "1" : "0");
                return;
            case "poll":
                WriteOutput(output, outputSize, InboundQueue.Dequeue() ?? "");
                return;
        }

        // JSON messages from SQF
        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;
            var type = root.GetProperty("t").GetString();

            switch (type)
            {
                case "connect":
                    var addr = root.GetProperty("addr").GetString() ?? "";
                    TcpClient.Log($"Connect: {addr}");
                    Client.Connect(addr);
                    WriteOutput(output, outputSize, Client.IsConnected ? "1" : "0");
                    break;

                case "head":
                case "state":
                    // Forward entire JSON to server
                    Client.Send(input);
                    break;

                case "rpc":
                    // Forward entire JSON to server
                    Client.Send(input);
                    break;

                case "ptt":
                    // Forward entire JSON to server
                    Client.Send(input);
                    break;

                default:
                    TcpClient.Log($"Unknown message type: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            TcpClient.Log($"RVExtension error: {ex.Message} | input: {input[..Math.Min(100, input.Length)]}");
        }

        WriteOutput(output, outputSize, "");
    }

    [UnmanagedCallersOnly(EntryPoint = "RVExtensionArgs")]
    public static int RVExtensionArgs(nint output, int outputSize, nint function, nint argv, int argc)
    {
        // Not used — all communication goes through simple form with JSON
        WriteOutput(output, outputSize, "");
        return 0;
    }

    private static unsafe void WriteOutput(nint output, int outputSize, string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var len = Math.Min(bytes.Length, outputSize - 1);
        Marshal.Copy(bytes, 0, output, len);
        ((byte*)output)[len] = 0;
    }

    private static string ReadString(nint ptr)
    {
        return Marshal.PtrToStringUTF8(ptr) ?? "";
    }
}
