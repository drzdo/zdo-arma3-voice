namespace ZdoArmaVoice.Server;

/// <summary>
/// Simple logger. Writes to console + server.log next to the exe.
/// No reflection, trim/AOT safe.
/// </summary>
public static class Log
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "server.log");
    private static readonly object Lock = new();

    static Log()
    {
        // Truncate on startup
        try { File.WriteAllText(LogPath, ""); } catch { }
    }

    public static void Info(string tag, string message) => Write("INFO", tag, message);
    public static void Warn(string tag, string message) => Write("WARN", tag, message);
    public static void Error(string tag, string message) => Write("ERR ", tag, message);

    public static void Sqf(int id, string sqf)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var line = $"[{timestamp}] [SQF] id={id}\n{sqf}\n---";
        Console.WriteLine($"[SQF] id={id} {sqf[..Math.Min(80, sqf.Length)]}...");
        Append(line);
    }

    private static void Write(string level, string tag, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level}] [{tag}] {message}";
        Console.WriteLine($"[{tag}] {message}");
        Append(line);
    }

    private static void Append(string line)
    {
        lock (Lock)
        {
            try { File.AppendAllText(LogPath, line + "\n"); } catch { }
        }
    }
}
