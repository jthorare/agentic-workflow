using Aw.Kernel;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.Launching;

/// <summary>The projector's progress with a script: each call to 'WaitFor' first runs the next step, if any (something that happens while the launcher waits).</summary>
internal sealed class HookedStatus(IProjectorStatus inner) : IProjectorStatus
{
    private readonly Queue<Func<Task>?> _script = new();

    public ViewsStatus Current => inner.Current;

    /// <summary>Queues what the next wait does first; null lets it pass.</summary>
    /// <param name="step">The step.</param>
    public void Then(Func<Task>? step) => _script.Enqueue(step);

    public async ValueTask<Result<Unit>> WaitFor(long sequence, TimeSpan limit, CancellationToken cancellationToken)
    {
        if (_script.TryDequeue(out var step) && step is not null)
        {
            await step().ConfigureAwait(false);
        }

        return await inner.WaitFor(sequence, limit, cancellationToken).ConfigureAwait(false);
    }
}
