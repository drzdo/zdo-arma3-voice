using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ArmaVoice.Server.Net;

/// <summary>
/// TCP server that listens for a single client connection (the Arma extension).
/// Reads newline-delimited messages with type-tag prefixes and dispatches to callbacks.
/// </summary>
public sealed class TcpBridge : IDisposable
{
    private readonly int _port;
    private TcpListener? _listener;
    private System.Net.Sockets.TcpClient? _client;
    private StreamWriter? _writer;
    private readonly object _writeLock = new();
    private volatile bool _disposed;

    public bool IsClientConnected { get; private set; }

    /// <summary>Fired when a state message arrives (raw SQF array string after "S|").</summary>
    public Action<string>? OnStateReceived { get; set; }

    /// <summary>Fired when an RPC response arrives (id, result string).</summary>
    public Action<int, string>? OnRpcResponse { get; set; }

    /// <summary>Fired when a PTT event arrives ("down"/"up", [x,y,z] position).</summary>
    public Action<string, float[]>? OnPttEvent { get; set; }

    /// <summary>Fired when a client connects. Used to trigger function registration.</summary>
    public Action? OnClientConnected { get; set; }

    public TcpBridge(int port = 9500)
    {
        _port = port;
    }

    /// <summary>
    /// Start listening for connections. Blocks until cancellation is requested.
    /// Accepts one client at a time; when a client disconnects, goes back to accepting.
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        Console.WriteLine($"[TcpBridge] Listening on 127.0.0.1:{_port}");

        ct.Register(() =>
        {
            _listener.Stop();
        });

        while (!ct.IsCancellationRequested && !_disposed)
        {
            try
            {
                Console.WriteLine("[TcpBridge] Waiting for client...");
                var client = await _listener.AcceptTcpClientAsync(ct);
                Console.WriteLine($"[TcpBridge] Client connected from {client.Client.RemoteEndPoint}");

                await HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpBridge] Accept error: {ex.Message}");
                if (!ct.IsCancellationRequested)
                {
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }
            }
        }

        Console.WriteLine("[TcpBridge] Stopped listening.");
    }

    /// <summary>
    /// Send an RPC call to the connected client. Format: "C|{id}|{sqf}\n"
    /// </summary>
    public void SendRpc(int id, string sqf)
    {
        lock (_writeLock)
        {
            if (!IsClientConnected || _writer == null)
            {
                Console.WriteLine($"[TcpBridge] Cannot send RPC (id={id}): no client connected");
                return;
            }

            try
            {
                _writer.WriteLine($"C|{id}|{sqf}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpBridge] Write error: {ex.Message}");
                IsClientConnected = false;
            }
        }
    }

    private async Task HandleClientAsync(System.Net.Sockets.TcpClient client, CancellationToken ct)
    {
        lock (_writeLock)
        {
            CleanupClient();
            _client = client;
            var stream = client.GetStream();
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true, NewLine = "\n" };
            IsClientConnected = true;
        }

        OnClientConnected?.Invoke();

        try
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!ct.IsCancellationRequested && !_disposed && client.Connected)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }

                if (line == null)
                {
                    // Client disconnected (EOF)
                    break;
                }

                if (line.Length < 2 || line[1] != '|')
                {
                    Console.WriteLine($"[TcpBridge] Malformed message (length={line.Length}): {line[..Math.Min(50, line.Length)]}");
                    continue;
                }

                var tag = line[0];
                var payload = line[2..];

                try
                {
                    DispatchMessage(tag, payload);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TcpBridge] Error dispatching '{tag}' message: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TcpBridge] Client read loop error: {ex.Message}");
        }
        finally
        {
            lock (_writeLock)
            {
                CleanupClient();
            }
            Console.WriteLine("[TcpBridge] Client disconnected.");
        }
    }

    private void DispatchMessage(char tag, string payload)
    {
        switch (tag)
        {
            case 'S':
                OnStateReceived?.Invoke(payload);
                break;

            case 'R':
            {
                // Format: "id|result"
                var separatorIndex = payload.IndexOf('|');
                if (separatorIndex < 0)
                {
                    Console.WriteLine($"[TcpBridge] Malformed RPC response: {payload[..Math.Min(50, payload.Length)]}");
                    return;
                }

                var idStr = payload[..separatorIndex];
                var result = payload[(separatorIndex + 1)..];

                if (int.TryParse(idStr, out var id))
                {
                    OnRpcResponse?.Invoke(id, result);
                }
                else
                {
                    Console.WriteLine($"[TcpBridge] Invalid RPC response id: {idStr}");
                }

                break;
            }

            case 'P':
            {
                // Format: "down|[x,y,z]" or "up|[x,y,z]"
                var separatorIndex = payload.IndexOf('|');
                if (separatorIndex < 0)
                {
                    Console.WriteLine($"[TcpBridge] Malformed PTT event: {payload[..Math.Min(50, payload.Length)]}");
                    return;
                }

                var direction = payload[..separatorIndex]; // "down" or "up"
                var posStr = payload[(separatorIndex + 1)..]; // "[x,y,z]"

                var pos = ParsePositionArray(posStr);
                OnPttEvent?.Invoke(direction, pos);
                break;
            }

            default:
                Console.WriteLine($"[TcpBridge] Unknown message tag: '{tag}'");
                break;
        }
    }

    /// <summary>
    /// Parse a simple SQF position array like "[3050,5020,0]" into float[].
    /// </summary>
    private static float[] ParsePositionArray(string s)
    {
        s = s.Trim();
        if (s.StartsWith('[') && s.EndsWith(']'))
        {
            s = s[1..^1];
        }

        var parts = s.Split(',');
        var result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
            {
                result[i] = val;
            }
        }

        return result;
    }

    private void CleanupClient()
    {
        IsClientConnected = false;
        try { _writer?.Dispose(); } catch { }
        try { _client?.Dispose(); } catch { }
        _writer = null;
        _client = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_writeLock)
        {
            CleanupClient();
        }

        try { _listener?.Stop(); } catch { }
        _listener = null;
    }
}
