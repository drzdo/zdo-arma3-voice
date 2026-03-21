using System.Collections.Concurrent;

namespace ArmaVoice.Extension;

/// <summary>
/// Thread-safe queue for passing messages between the TCP background thread
/// and the main Arma thread (which calls RVExtension).
/// </summary>
public sealed class CommandQueue
{
    private readonly ConcurrentQueue<string> _queue = new();

    /// <summary>
    /// Enqueue a message (called from the TCP reader thread).
    /// </summary>
    public void Enqueue(string message)
    {
        _queue.Enqueue(message);
    }

    /// <summary>
    /// Try to dequeue a single message (called from the main Arma thread via poll).
    /// Returns the message, or null if the queue is empty.
    /// </summary>
    public string? Dequeue()
    {
        if (_queue.TryDequeue(out var message))
            return message;
        return null;
    }
}
