using System.Net.Sockets;

namespace ZdoArmaVoice.Extension;

/// <summary>
/// Manages a persistent TCP connection to the C# server on a background thread.
/// Reads newline-delimited messages from the server and pushes them into an inbound CommandQueue.
/// </summary>
public sealed class TcpClient : IDisposable
{
    private readonly CommandQueue _inbound;
    private System.Net.Sockets.TcpClient? _client;
    private NetworkStream? _stream;
    private StreamWriter? _writer;
    private Thread? _readerThread;
    private volatile bool _isConnected;
    private volatile bool _disposed;
    private readonly object _writeLock = new();
    private readonly object _connectLock = new();

    public bool IsConnected => _isConnected;

    internal static void Log(string msg)
    {
        try
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n";
            File.AppendAllText("zdo_arma_voice_ext.log", line);
        }
        catch { }
    }

    public TcpClient(CommandQueue inbound)
    {
        _inbound = inbound;
    }

    /// <summary>
    /// Parse "host:port" and connect to the server. Starts the background reader thread.
    /// </summary>
    public void Connect(string hostPort)
    {
        lock (_connectLock)
        {
            Log($"Connect called with: '{hostPort}'");
            CleanupConnection();

            var parts = hostPort.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
            {
                Log($"Connect failed: invalid host:port format '{hostPort}'");
                return;
            }

            var host = parts[0];
            Log($"Connecting to {host}:{port}...");

            try
            {
                _client = new System.Net.Sockets.TcpClient();
                _client.Connect(host, port);
                _stream = _client.GetStream();
                _writer = new StreamWriter(_stream, System.Text.Encoding.UTF8)
                {
                    AutoFlush = true,
                    NewLine = "\n"
                };
                _isConnected = true;

                _readerThread = new Thread(ReaderLoop)
                {
                    IsBackground = true,
                    Name = "ZdoArmaVoice.TcpReader"
                };
                _readerThread.Start();
                Log($"Connected to {host}:{port}");
            }
            catch (Exception ex)
            {
                Log($"Connect failed: {ex.Message}");
                CleanupConnection();
            }
        }
    }

    /// <summary>
    /// Disconnect from the server and clean up resources.
    /// </summary>
    public void Disconnect()
    {
        lock (_connectLock)
        {
            Log("Disconnect called");
            CleanupConnection();
        }
    }

    /// <summary>
    /// Send a newline-delimited message to the server. Thread-safe.
    /// </summary>
    public void Send(string message)
    {
        lock (_writeLock)
        {
            if (!_isConnected || _writer == null)
                return;

            try
            {
                _writer.WriteLine(message);
            }
            catch (Exception ex)
            {
                Log($"Send error: {ex.Message}");
                _isConnected = false;
            }
        }
    }

    /// <summary>
    /// Background thread: reads newline-delimited JSON messages from the server
    /// and enqueues them for SQF to poll. SQF will use fromJSON to parse.
    /// </summary>
    private void ReaderLoop()
    {
        try
        {
            using var reader = new StreamReader(_stream!, System.Text.Encoding.UTF8);

            while (_isConnected && !_disposed)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;

                // Server sends JSON objects — pass through directly to SQF
                // SQF will use fromJSON to parse
                if (line.StartsWith("{"))
                {
                    _inbound.Enqueue(line);
                    Log($"Received: {line[..Math.Min(80, line.Length)]}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Reader error: {ex.Message}");
        }
        finally
        {
            Log("Reader loop ended, disconnected");
            _isConnected = false;
        }
    }

    private void CleanupConnection()
    {
        _isConnected = false;

        try { _writer?.Dispose(); } catch { }
        try { _stream?.Dispose(); } catch { }
        try { _client?.Dispose(); } catch { }

        _writer = null;
        _stream = null;
        _client = null;

        // Wait briefly for reader thread to exit
        if (_readerThread != null && _readerThread.IsAlive)
        {
            _readerThread.Join(timeout: TimeSpan.FromSeconds(2));
        }
        _readerThread = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Disconnect();
    }
}
