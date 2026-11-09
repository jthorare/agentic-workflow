using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.Commands;

/// <summary>A slice's way into the <see cref="CommandGateLock"/> (a handler registered with AddUngatedCommand): the same 5 s wait, and a stop ends it.</summary>
/// <param name="gate">The process-wide gate.</param>
/// <param name="clock">The clock the 5 s limit runs on.</param>
internal sealed class CommandGateAccess(CommandGateLock gate, TimeProvider clock) : ICommandGate
{
    public async ValueTask<IDisposable?> Enter(CancellationToken cancellationToken)
    {
        using var running = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, gate.Stopping);
        try
        {
            return await gate.Enter(clock, running.Token).ConfigureAwait(false) ? new Hold(gate) : null;
        }
        catch (OperationCanceledException) when (gate.Stopping.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return null;   // the process is stopping: a refusal like a busy gate
        }
    }

    private sealed class Hold(CommandGateLock gate) : IDisposable
    {
        private int _left;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _left, 1) == 0)
            {
                gate.Exit();
            }
        }
    }
}
