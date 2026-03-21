using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArmaVoice.Server.Net;

/// <summary>
/// TCP server that listens for a single client connection (the Arma extension).
/// All messages are newline-delimited JSON with a "t" field for type.
/// </summary>
public sealed class TcpBridge : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private TcpListener? _listener;
    private System.Net.Sockets.TcpClient? _client;
    private StreamWriter? _writer;
    private readonly object _writeLock = new();
    private volatile bool _disposed;

    public bool IsClientConnected { get; private set; }

    /// <summary>Fired when a state message arrives (JSON with p, d, u fields).</summary>
    public Action<JsonElement>? OnStateReceived { get; set; }

    /// <summary>Fired when an RPC response arrives (id, result as JsonElement).</summary>
    public Action<int, JsonElement>? OnRpcResponse { get; set; }

    /// <summary>Fired when a PTT event arrives ("down"/"up", [x,y,z] position).</summary>
    public Action<string, float[]>? OnPttEvent { get; set; }

    /// <summary>Fired when a client connects.</summary>
    public Action? OnClientConnected { get; set; }

    public TcpBridge(string host = "0.0.0.0", int port = 9500)
    {
        _host = host;
        _port = port;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var ip = IPAddress.Parse(_host);
        _listener = new TcpListener(ip, _port);
        _listener.Start();
        Console.WriteLine($"[TcpBridge] Listening on {_host}:{_port}");

        ct.Register(() => _listener.Stop());

        while (!ct.IsCancellationRequested && !_disposed)
        {
            try
            {
                Console.WriteLine("[TcpBridge] Waiting for client...");
                var client = await _listener.AcceptTcpClientAsync(ct);
                Console.WriteLine($"[TcpBridge] Client connected from {client.Client.RemoteEndPoint}");
                await HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpBridge] Accept error: {ex.Message}");
                if (!ct.IsCancellationRequested)
                    await Task.Delay(1000, ct).ConfigureAwait(false);
            }
        }

        Console.WriteLine("[TcpBridge] Stopped listening.");
    }

    /// <summary>
    /// Send a JSON RPC call to the extension. SQF will poll and fromJSON it.
    /// </summary>
    public void SendRpc(int id, string sqf)
    {
        var obj = new JsonObject { ["id"] = id, ["sqf"] = sqf };
        SendLine(obj.ToJsonString());
    }

    private void SendLine(string line)
    {
        lock (_writeLock)
        {
            if (!IsClientConnected || _writer == null) return;
            try
            {
                _writer.WriteLine(line);
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
                try { line = await reader.ReadLineAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch (IOException) { break; }

                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var type = root.GetProperty("t").GetString();

                    switch (type)
                    {
                        case "state":
                            OnStateReceived?.Invoke(root);
                            break;

                        case "rpc":
                            var id = root.GetProperty("id").GetInt32();
                            var result = root.GetProperty("r");
                            OnRpcResponse?.Invoke(id, result.Clone());
                            break;

                        case "ptt":
                            var dir = root.GetProperty("dir").GetString() ?? "";
                            var posArr = root.GetProperty("pos");
                            var pos = new float[posArr.GetArrayLength()];
                            for (int i = 0; i < pos.Length; i++)
                                pos[i] = posArr[i].GetSingle();
                            OnPttEvent?.Invoke(dir, pos);
                            break;

                        default:
                            Console.WriteLine($"[TcpBridge] Unknown type: {type}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TcpBridge] Parse error: {ex.Message} | {line[..Math.Min(80, line.Length)]}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TcpBridge] Client error: {ex.Message}");
        }
        finally
        {
            lock (_writeLock) { CleanupClient(); }
            Console.WriteLine("[TcpBridge] Client disconnected.");
        }
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
        lock (_writeLock) { CleanupClient(); }
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }
}
