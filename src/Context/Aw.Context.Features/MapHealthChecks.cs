using Aw.Context.Core.Map;
using Aw.Context.Features.CheckRetention;
using Aw.Context.Features.CheckStore;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The doctor checks on what the map leaves on disk: retained exports and versions, and the store's write-ahead log.</summary>
internal sealed class MapHealthChecks(IService<CheckRetentionRequest, MapHealth> retention, IService<CheckStoreRequest, MapHealth> store)
{
    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) =>
        retention.Handle(new CheckRetentionRequest(), cancellationToken);

    public ValueTask<Result<MapHealth>> CheckStore(CancellationToken cancellationToken) =>
        store.Handle(new CheckStoreRequest(), cancellationToken);
}
