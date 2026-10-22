using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The human's list (API spec §3.1, §3.6): the 'needs_you' rows the caller may see (a session its repo's and the repo-less ones) in
/// <see cref="NeedsYouOrder"/>. With no row the sentence counts the running and queued items the caller sees; stale views replace it.
/// </summary>
/// <param name="views">The 'needs_you' and 'items' views.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller, whose repo scope applies.</param>
/// <param name="live">The rows read at request time, not from the view (the managed Onyx's notice and the setup rows).</param>
/// <param name="clock">The time the timeouts are measured at.</param>
/// <param name="joins">Adds the stop and the timeout to the desk and park rows.</param>
internal sealed class GetNeedsYouService(IViewReader views, IProjectorStatus status, ICallerContextAccessor callers, NeedsYouLiveRows live, TimeProvider clock, NeedsYouJoins joins) : IService<GetNeedsYouRequest, GetNeedsYouResponse>
{
    public async ValueTask<Result<GetNeedsYouResponse>> Handle(GetNeedsYouRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("the needs-you list needs a caller");
        }

        var current = status.Current;
        var records = await views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, cancellationToken).ConfigureAwait(false);
        if (records.IsError)
        {
            return records.Error;
        }

        var rows = await joins.Apply(NeedsYouOrder.Sort([.. records.Value.Select(static r => r.Row).Where(r => ItemScope.VisibleOrRepoless(caller, r.Repo)), .. await live.Read(caller, cancellationToken).ConfigureAwait(false)]), clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var empty = current.Text;
        if (empty is null && rows.Count == 0)
        {
            var counted = await Counts(caller, cancellationToken).ConfigureAwait(false);
            if (counted.IsError)
            {
                return counted.Error;
            }

            empty = EmptyText.NeedsYou(counted.Value.Running, counted.Value.Queued);
        }

        return new GetNeedsYouResponse(rows, empty, current.AsOf);
    }

    private async ValueTask<Result<(int Running, int Queued)>> Counts(CallerContext caller, CancellationToken cancellationToken)
    {
        var items = await views.List<ItemRow>(ViewNames.Items, cancellationToken).ConfigureAwait(false);
        if (items.IsError)
        {
            return items.Error;
        }

        var seen = items.Value.Where(r => ItemScope.Visible(caller, r.Repo)).Select(static r => QueueGroups.GroupOf(r.State)).ToList();
        return (seen.Count(static g => g == QueueGroup.Running), seen.Count(static g => g == QueueGroup.Queued));
    }
}
