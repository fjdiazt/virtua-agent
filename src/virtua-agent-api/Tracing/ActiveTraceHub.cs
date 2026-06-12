using System.Collections.Concurrent;
using System.Threading.Channels;

namespace VirtuaAgent.Tracing;

public sealed class ActiveTraceHub
{
    private readonly ConcurrentDictionary<string, List<Channel<TraceEventRecord>>> _subscribers = new();

    public async Task PublishAsync(string runId, TraceEventRecord traceEvent)
    {
        if (!_subscribers.TryGetValue(runId, out var subscribers)) return;

        Channel<TraceEventRecord>[] snapshot;
        lock (subscribers)
        {
            snapshot = subscribers.ToArray();
        }

        foreach (var channel in snapshot)
        {
            await channel.Writer.WriteAsync(traceEvent);
        }
    }

    public IAsyncEnumerable<TraceEventRecord> Subscribe(string runId, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<TraceEventRecord>();
        var subscribers = _subscribers.GetOrAdd(runId, _ => []);

        lock (subscribers)
        {
            subscribers.Add(channel);
        }

        cancellationToken.Register(() =>
        {
            channel.Writer.TryComplete();
            lock (subscribers)
            {
                subscribers.Remove(channel);
            }
        });

        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}
