namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>
/// The "read every cursor forward now" request (#2258, PR 11). It starts pending (a control-plane start catches up) and the scheduler host sets it again when
/// <see cref="WakeDetector"/> sees the machine wake. The poll service takes it at the start of a run; a catch-up that fails puts it back, so the next run tries again.
/// One singleton is shared by the host and the service (the service itself is scoped per run).
/// </summary>
public sealed class PollWake
{
    private int _pending = 1;

    /// <summary>Asks for a catch-up on the next poll run.</summary>
    public void Request() => Volatile.Write(ref _pending, 1);

    /// <summary>Takes the request.</summary>
    /// <returns>True when a catch-up was requested since the last take; the request is cleared.</returns>
    public bool TryTake() => Interlocked.Exchange(ref _pending, 0) == 1;
}
