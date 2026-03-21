using System.Collections.Concurrent;
using ArmaVoice.Server.Net;

namespace ArmaVoice.Server.Game;

/// <summary>
/// Sends SQF RPCs through TcpBridge and tracks pending responses by id.
/// id=0 is fire-and-forget (no response expected).
/// </summary>
public class RpcClient
{
    private readonly TcpBridge _bridge;
    private int _nextId = 1;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _pending = new();

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public RpcClient(TcpBridge bridge)
    {
        _bridge = bridge;
    }

    /// <summary>
    /// Send an RPC with a unique id and await the response.
    /// Throws TimeoutException if no response within 5 seconds.
    /// Throws OperationCanceledException if the cancellation token fires.
    /// </summary>
    public async Task<string> CallAsync(string sqf, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        try
        {
            _bridge.SendRpc(id, sqf);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(DefaultTimeout);

            // Register cancellation to fault the TCS
            await using var registration = timeoutCts.Token.Register(() =>
            {
                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(ct);
                }
                else
                {
                    tcs.TrySetException(new TimeoutException($"RPC {id} timed out after {DefaultTimeout.TotalSeconds}s: {sqf[..Math.Min(60, sqf.Length)]}"));
                }
            });

            return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Send a fire-and-forget RPC (id=0). Does not await a response.
    /// </summary>
    public void Fire(string sqf)
    {
        _bridge.SendRpc(0, sqf);
    }

    /// <summary>
    /// Complete the pending TCS for the given id. Called by TcpBridge.OnRpcResponse.
    /// </summary>
    public void HandleResponse(int id, string result)
    {
        if (id == 0)
        {
            // Fire-and-forget responses are unexpected but harmless
            return;
        }

        if (_pending.TryGetValue(id, out var tcs))
        {
            tcs.TrySetResult(result);
        }
        else
        {
            Console.WriteLine($"[RpcClient] No pending request for response id={id}");
        }
    }
}
