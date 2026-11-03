using Aw.Context.Core.Map;
using Aw.Context.Features.CheckRetention;
using Aw.Context.Features.CheckStore;
using Aw.Context.Features.RunUpkeep;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The checks and upkeep on what the map leaves on disk: retained exports and versions, and the store's write-ahead log and planner statistics.</summary>
internal sealed class MapHealthChecks(IService<CheckRetentionRequest, MapHealth> retention, IService<CheckStoreRequest, MapHealth> store, IService<RunUpkeepRequest, StoreUpkeep?> upkeep)
{
    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) =>
        retention.Handle(new CheckRetentionRequest(), cancellationToken);

    public ValueTask<Result<MapHealth>> CheckStore(CancellationToken cancellationToken) =>
        store.Handle(new CheckStoreRequest(), cancellationToken);

    public ValueTask<Result<StoreUpkeep?>> Upkeep(CancellationToken cancellationToken) =>
        upkeep.Handle(new RunUpkeepRequest(), cancellationToken);
}
