using System.Runtime.InteropServices;

namespace ArmaVoice.Extension;

/// <summary>
/// Main entry point. Exports the 3 Arma 3 extension functions using [UnmanagedCallersOnly].
/// </summary>
public static class Exports
{
    private static readonly CommandQueue InboundQueue = new();
    private static readonly TcpClient Client = new(InboundQueue);

    // -----------------------------------------------------------------------
    // Arma 3 callExtension entry points
    // -----------------------------------------------------------------------

    [UnmanagedCallersOnly(EntryPoint = "RVExtensionVersion")]
    public static void RVExtensionVersion(nint output, int outputSize)
    {
        TcpClient.Log("RVExtensionVersion called");
        WriteOutput(output, outputSize, "1.0.0");
    }

    [UnmanagedCallersOnly(EntryPoint = "RVExtension")]
    public static void RVExtension(nint output, int outputSize, nint function)
    {
        var func = ReadString(function);

        switch (func)
        {
            case "status":
                WriteOutput(output, outputSize, Client.IsConnected ? "1" : "0");
                break;

            case "poll":
                var message = InboundQueue.Dequeue();
                WriteOutput(output, outputSize, message ?? "");
                break;

            default:
                WriteOutput(output, outputSize, "");
                break;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "RVExtensionArgs")]
    public static int RVExtensionArgs(nint output, int outputSize, nint function, nint argv, int argc)
    {
        var func = ReadString(function);
        var args = ReadArgs(argv, argc);

        switch (func)
        {
            case "connect":
                if (args.Length >= 1)
                {
                    TcpClient.Log($"RVExtensionArgs: connect({args[0]})");
                    Client.Connect(args[0]);
                    WriteOutput(output, outputSize, Client.IsConnected ? "1" : "0");
                }
                break;

            case "state":
                if (args.Length >= 1)
                    Client.Send($"S|{args[0]}");
                break;

            case "respond":
                if (args.Length >= 2)
                    Client.Send($"R|{args[0]}|{args[1]}");
                break;

            case "ptt":
                if (args.Length >= 2)
                    Client.Send($"P|{args[0]}|{args[1]}");
                break;
        }

        return 0;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Write a C# string to the unmanaged output buffer as UTF-8, null-terminated.
    /// </summary>
    private static unsafe void WriteOutput(nint output, int outputSize, string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var len = Math.Min(bytes.Length, outputSize - 1);
        Marshal.Copy(bytes, 0, output, len);
        ((byte*)output)[len] = 0;
    }

    /// <summary>
    /// Read a null-terminated ANSI string from an unmanaged pointer.
    /// </summary>
    private static string ReadString(nint ptr)
    {
        return Marshal.PtrToStringAnsi(ptr) ?? "";
    }

    /// <summary>
    /// Read an argv array. The nint is a pointer to an array of nint pointers to strings.
    /// </summary>
    private static unsafe string[] ReadArgs(nint argv, int argc)
    {
        var args = new string[argc];
        var ptrs = (nint*)argv;
        for (int i = 0; i < argc; i++)
        {
            var s = ReadString(ptrs[i]);
            // SQF wraps string arguments in quotes when converting for callExtension
            if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
                s = s[1..^1];
            args[i] = s;
        }
        return args;
    }
}
