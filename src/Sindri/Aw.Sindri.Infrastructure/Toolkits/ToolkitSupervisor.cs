using Aw.Kernel;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>Keeps toolkit processes running (see <see cref="IToolkitRuntime"/>).</summary>
/// <param name="launcher">Starts processes.</param>
/// <param name="store">Takes the 'failed' state.</param>
/// <param name="clock">The clock the backoff and the crash window use.</param>
#pragma warning disable MA0182 // the red stub: registered with the green commit
internal sealed class ToolkitSupervisor(IToolkitProcessLauncher launcher, IToolkitStore store, TimeProvider clock) : IToolkitRuntime, IAsyncDisposable
{
    public ValueTask<Result<Unit>> Start(Toolkit toolkit, CancellationToken cancellationToken)
    {
        _ = (launcher, store, clock);   // the red stub
        throw new NotSupportedException();
    }

    public ValueTask<Result<Unit>> Stop(string name, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
#pragma warning restore MA0182
