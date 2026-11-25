using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Features;

/// <summary>
/// The engine's singleton <see cref="IGraphMemoryPort"/>: the graph blocks are singletons and <see cref="WorkflowMemoryPort"/> is scoped, so the registration lambda opens a scope per call
/// and this port calls through it (ARCH-DI-001: only a registration lambda resolves from the provider).
/// </summary>
/// <param name="recall">Recalls through a scoped <see cref="WorkflowMemoryPort"/>.</param>
/// <param name="remember">Writes through one.</param>
/// <param name="forget">Forgets through one.</param>
internal sealed class ScopedMemoryPort(
    Func<MemoryToRecall, CancellationToken, ValueTask<Result<MemoryRecalled>>> recall,
    Func<MemoryToRemember, CancellationToken, ValueTask<Result<MemoryRemembered>>> remember,
    Func<MemoryToForget, CancellationToken, ValueTask<Result<int>>> forget) : IGraphMemoryPort
{
    public ValueTask<Result<MemoryRecalled>> Recall(MemoryToRecall request, CancellationToken cancellationToken) => recall(request, cancellationToken);

    public ValueTask<Result<MemoryRemembered>> Remember(MemoryToRemember request, CancellationToken cancellationToken) => remember(request, cancellationToken);

    public ValueTask<Result<int>> Forget(MemoryToForget request, CancellationToken cancellationToken) => forget(request, cancellationToken);
}
