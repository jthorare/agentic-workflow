using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Features.GetPinMoves;
using Aw.Context.Features.HeldMap;
using Aw.Context.Features.HoldMap;
using Aw.Context.Features.ReleaseMap;
using Aw.Context.Features.ShowPublishStatus;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The publish half of the map facade (F1P): the hold, its release and the reads.</summary>
internal sealed class MapPublishing(
    IService<HeldMapRequest, MapPublishState> held,
    IService<HoldMapRequest, MapHold> hold,
    IService<ReleaseMapRequest, Unit> release,
    IService<ShowPublishStatusRequest, MapPublishView> status,
    IService<GetPinMovesRequest, IReadOnlyList<PinMove>> moves)
{
    public ValueTask<Result<MapPublishState>> Held(ContextScope scope, CancellationToken cancellationToken) =>
        held.Handle(new HeldMapRequest(scope), cancellationToken);

    public ValueTask<Result<MapHold>> Hold(MapHold value, ContextScope scope, CancellationToken cancellationToken) =>
        hold.Handle(new HoldMapRequest(value, scope), cancellationToken);

    public ValueTask<Result<Unit>> Release(int version, ContextScope scope, CancellationToken cancellationToken) =>
        release.Handle(new ReleaseMapRequest(version, scope), cancellationToken);

    public ValueTask<Result<MapPublishView>> PublishStatus(ContextScope scope, CancellationToken cancellationToken) =>
        status.Handle(new ShowPublishStatusRequest(scope), cancellationToken);

    public ValueTask<Result<IReadOnlyList<PinMove>>> PinMoves(MapVersion from, MapVersion to, ContextScope scope, CancellationToken cancellationToken) =>
        moves.Handle(new GetPinMovesRequest(scope, from, to), cancellationToken);
}
