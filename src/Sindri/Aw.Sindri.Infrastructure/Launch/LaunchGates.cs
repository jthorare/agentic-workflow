using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// One turn at a time per launch id: two calls for the same launch never run together. A gate lives while anyone holds it or waits on it and is
/// removed when the last of them leaves, so it neither leaks nor disappears under a waiter.
/// </summary>
internal sealed class LaunchGates
{
    private readonly Dictionary<LaunchId, Gate> _gates = [];
    private readonly Lock _lock = new();

    /// <summary>Waits for the launch's turn; dispose the result to give it up.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The turn.</returns>
    internal async ValueTask<IDisposable> Turn(LaunchId launch, CancellationToken cancellationToken)
    {
        var gate = Enter(launch);
        try
        {
            await gate.Turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Leave(launch, gate, held: false);
            throw;
        }

        return new Held(this, launch, gate);
    }

    private Gate Enter(LaunchId launch)
    {
        lock (_lock)
        {
            if (!_gates.TryGetValue(launch, out var gate))
            {
                gate = new Gate();
                _gates[launch] = gate;
            }

            gate.Users++;
            return gate;
        }
    }

    private void Leave(LaunchId launch, Gate gate, bool held)
    {
        if (held)
        {
            _ = gate.Turn.Release();
        }

        lock (_lock)
        {
            if (--gate.Users == 0)
            {
                _ = _gates.Remove(launch);   // idle: nobody holds it or waits on it, so removing it can't let two launches of one id run
                gate.Turn.Dispose();
            }
        }
    }

    internal int Open
    {
        get
        {
            lock (_lock)
            {
                return _gates.Count;
            }
        }
    }

    private sealed class Gate
    {
        public SemaphoreSlim Turn { get; } = new(1, 1);

        public int Users { get; set; }
    }

    private sealed class Held(LaunchGates owner, LaunchId launch, Gate gate) : IDisposable
    {
        private int _left;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _left, 1) == 0)
            {
                owner.Leave(launch, gate, held: true);
            }
        }
    }
}
