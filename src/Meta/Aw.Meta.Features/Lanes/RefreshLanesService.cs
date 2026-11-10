using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Lanes;

/// <summary>
/// Maps each configured lane to the live session working it (a session named "team-lane", else one that announced the lane)
/// and saves the map when it changed. A changed map raises the lane map version that lanes see through board-line.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="refresher">Maps and saves.</param>
/// <param name="clock">Dates a changed map.</param>
/// <param name="standing">How each non-Claude lane stands.</param>
internal sealed class RefreshLanesService(IMetaConfig config, LaneRefresher refresher, TimeProvider clock, LaneHealthReader standing) : IService<RefreshLanesRequest, LanesResponse>
{
    public async ValueTask<Result<LanesResponse>> Handle(RefreshLanesRequest request, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var refreshed = await refresher.Run(team.Value, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (refreshed.IsError)
        {
            return refreshed.Error;
        }

        var rows = await standing.Read(team.Value, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : LanesReport.Of(team.Value, refreshed.Value, rows.Value.Count == 0 ? null : rows.Value);
    }
}
