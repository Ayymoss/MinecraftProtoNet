using System.Collections.Concurrent;

namespace MinecraftProtoNet.Core.Utilities;

/// <summary>
/// A message pump that keeps an async method pinned to the thread it started on.
///
/// The game loop needs a stable thread identity for two reasons. Vanilla's PacketProcessor is constructed
/// with the render thread and compares against it to decide whether a packet handler may run inline
/// (Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/util/thread/PacketProcessor.java:18-24), and our
/// PacketProcessor.IsSameThread does the same. Without a context, an `async` thread body abandons its thread
/// at the first await and every later resumption lands on an arbitrary thread-pool thread, so that check is
/// false for ever and each loop iteration additionally picks up thread-pool scheduling latency.
///
/// Usage is deliberately narrow: Run() blocks the calling thread, pumping continuations until the supplied
/// task completes. Everything the loop awaits therefore resumes here, on this thread, in order.
/// </summary>
public static class SingleThreadedSynchronizationContext
{
    public static void Run(Func<Task> asyncMethod)
    {
        ArgumentNullException.ThrowIfNull(asyncMethod);

        var previous = SynchronizationContext.Current;
        var context = new PumpContext();
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            var task = asyncMethod();

            // Stop the pump once the loop finishes, however it finishes. Posting the completion rather than
            // setting a flag keeps the shutdown ordered behind any continuations already queued.
            task.ContinueWith(_ => context.Complete(), TaskScheduler.Default);

            context.Pump();

            // Surface loop faults to the thread that owns the loop rather than swallowing them into a
            // dropped Task. GetAwaiter().GetResult() unwraps AggregateException to the real exception.
            task.GetAwaiter().GetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class PumpContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            ArgumentNullException.ThrowIfNull(d);

            try
            {
                _queue.Add((d, state));
            }
            catch (InvalidOperationException)
            {
                // Queue already completed (or disposed — ObjectDisposedException derives from this) — the
                // loop is shutting down and this continuation no longer matters. Dropping it is correct;
                // throwing here would surface on an unrelated thread.
            }
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            // Only legal from the pump thread itself; anything else risks a deadlock, since the caller would
            // be waiting on a queue only this thread drains.
            ArgumentNullException.ThrowIfNull(d);
            d(state);
        }

        public void Pump()
        {
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }

        public void Complete()
        {
            try
            {
                _queue.CompleteAdding();
            }
            catch (ObjectDisposedException)
            {
                // Already torn down.
            }
        }
    }
}
