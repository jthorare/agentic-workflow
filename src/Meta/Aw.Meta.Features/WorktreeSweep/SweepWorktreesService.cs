using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Features.WorktreeSweep;

/// <summary>Removes the worktrees whose verdict is removable: all of them, or one pull request's.</summary>
/// <param name="config">The team config.</param>
/// <param name="sweeper">Removes them and keeps the log line.</param>
internal sealed class SweepWorktreesService(IMetaConfig config, WorktreeSweeper sweeper) : IService<SweepWorktreesRequest, SweepWorktreesResponse>
{
    public async ValueTask<Result<SweepWorktreesResponse>> Handle(SweepWorktreesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var scope = request.Pr is { } pr ? WorktreeScope.ForPrs([pr]) : request.IncludeNoPr ? WorktreeScope.AllIncludingNoPr : WorktreeScope.All;
        var swept = await sweeper.Sweep(team.Value, scope, request.DryRun ? SweepMode.DryRun : SweepMode.Remove, "sweep", cancellationToken).ConfigureAwait(false);
        return swept.IsError ? swept.Error : new SweepWorktreesResponse(swept.Value.DryRun, swept.Value.Removed, swept.Value.Kept, swept.Value.Failed);
    }
}
