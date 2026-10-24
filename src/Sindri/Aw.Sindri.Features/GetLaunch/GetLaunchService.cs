using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetLaunch;

/// <summary>
/// Reads a launch (UI spec E-1): a human or service sees every launch, a session only launches of items in its own repo; any other is SND-LAUNCH-404,
/// never a hint that it exists.
/// </summary>
/// <param name="launches">The 'launches' view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class GetLaunchService(ILaunchViews launches, IProjectorStatus status, ICallerContextAccessor callers) : IService<GetLaunchRequest, LaunchView>
{
    public async ValueTask<Result<LaunchView>> Handle(GetLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("a launch needs a caller");
        }

        var asOf = status.Current.AsOf;
        var found = await launches.Get(request.LaunchId, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not { } row)
        {
            return SindriErrors.LaunchNotFound.With("No such launch (fix: the id the open answered with)");
        }

        return new LaunchView(row.Id, row.Item, row.Mode, row.State, row.Target, row.ChosenBy, row.RequestedAt, row.OutcomeAt, row.Code, row.Message, row.Stage, row.ResumableId,
            row.TabTitle, row.Viewer, row.Late, row.Attach, row.CatalogSource, row.ArgvHash, row.Ended, row.Step, asOf);
    }
}
