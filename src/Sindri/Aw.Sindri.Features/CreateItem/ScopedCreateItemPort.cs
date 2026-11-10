using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>
/// The engine's singleton <see cref="ICreateItemPort"/>: the graph blocks are singletons and <see cref="WorkflowItemMaker"/> is scoped, so the registration lambda opens a scope per item
/// and this port calls through it (ARCH-DI-001: only a registration lambda resolves from the provider).
/// </summary>
/// <param name="inScope">Makes one item through a scoped <see cref="WorkflowItemMaker"/>.</param>
internal sealed class ScopedCreateItemPort(Func<ItemToCreate, CancellationToken, ValueTask<Result<string>>> inScope) : ICreateItemPort
{
    public ValueTask<Result<string>> Create(ItemToCreate item, CancellationToken cancellationToken) => inScope(item, cancellationToken);
}
