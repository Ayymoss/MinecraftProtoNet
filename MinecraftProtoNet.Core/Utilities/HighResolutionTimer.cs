using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MinecraftProtoNet.Core.Utilities;

/// <summary>
/// Raises the system timer resolution so a ~50ms sleep actually lasts ~50ms.
///
/// Windows' default timer quantum is 15.625ms, and <c>Task.Delay</c>/<c>Thread.Sleep</c> round UP to the next
/// quantum. A game loop asking for the ~45ms that remains after tick processing therefore sleeps 62.5ms
/// (4 x 15.625), so the client runs at 16 ticks/sec instead of 20 — a silent 20% divergence from vanilla in
/// movement rate and outgoing packet cadence.
///
/// This was invisible for a long time because the tick counter used to report TPS is
/// <c>ClientTickCounter</c>, which <see cref="MinecraftProtoNet.Core.State.TickManager"/> syncs from the
/// server's world age — so it reported ~20 no matter how slowly our own loop ran.
///
/// timeBeginPeriod is process-wide and reference-counted by the OS; every call must be paired with
/// timeEndPeriod. Raising it costs a little extra power/scheduler churn, which is the accepted trade for a
/// client that has to keep the server's tick rate.
/// </summary>
public static class HighResolutionTimer
{
    [SupportedOSPlatform("windows")]
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [SupportedOSPlatform("windows")]
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

    private const uint TargetPeriodMs = 1;
    private static readonly Lock Gate = new();
    private static int _refCount;

    /// <summary>
    /// Requests 1ms timer resolution until the returned handle is disposed. No-op on non-Windows, where
    /// timer granularity is already ~1ms.
    /// </summary>
    public static IDisposable Acquire() => new Handle();

    private sealed class Handle : IDisposable
    {
        private bool _released;

        public Handle()
        {
            if (!OperatingSystem.IsWindows()) return;

            lock (Gate)
            {
                if (_refCount == 0) TimeBeginPeriod(TargetPeriodMs);
                _refCount++;
            }
        }

        public void Dispose()
        {
            if (_released || !OperatingSystem.IsWindows()) return;
            _released = true;

            lock (Gate)
            {
                _refCount--;
                if (_refCount == 0) TimeEndPeriod(TargetPeriodMs);
            }
        }
    }
}
