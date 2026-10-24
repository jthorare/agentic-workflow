using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>
/// Resolves the instance a reference names (its id, its address, or a bare item: spec §6.1, §6.3), asks its workflow for its view (the
/// 'GetInstance' Query; SND-WF-001 when there is no such instance), and answers the API's <see cref="InstanceDetail"/> (API spec §3.8).
/// The title is the item's, else the address; the parent is
/// resolved by its id; the counts come from the timeline, decisions and requests views; 'open' is the instance's own needs-you row's (q65).
/// </summary>
/// <param name="directory">Resolves the reference to the instance's address.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="views">The items, timeline, decisions and requests views.</param>
/// <param name="callers">The caller; a read needs one.</param>
/// <param name="contexts">Builds the detail context at the time of the read, with the recipe's display name (null serves none).</param>
/// <param name="stepRuns">The instance's Step runs, folded from its ledger events.</param>
internal sealed class GetInstanceService(IInstanceDirectory directory, IInstanceReader runtime, IViewReader views, ICallerContextAccessor callers, DetailContexts contexts, StepRunReader stepRuns)
    : IService<GetInstanceRequest, InstanceDetail>
{
    public async ValueTask<Result<InstanceDetail>> Handle(GetInstanceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("an instance read needs a caller");
        }

        var address = await directory.Resolve(request.Instance, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return InstanceScope.Unknown(address.Error, request.Instance);
        }

        var view = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return InstanceScope.Unknown(view.Error, request.Instance);
        }

        var title = await TitleOf(view.Value, cancellationToken).ConfigureAwait(false);
        var counts = await Counts(view.Value.ItemId, cancellationToken).ConfigureAwait(false);
        if (counts.IsError)
        {
            return counts.Error;
        }

        var parent = await Parent(view.Value, cancellationToken).ConfigureAwait(false);
        var runs = await stepRuns.Read(view.Value, cancellationToken).ConfigureAwait(false);
        if (runs.IsError)
        {
            return runs.Error;
        }

        var context = await contexts.Of(view.Value, new DetailInputs(title, parent, counts.Value, runs.Value), cancellationToken).ConfigureAwait(false);
        return InstanceViews.Detail(view.Value, context) with { Open = await OpenOf(view.Value.Address, cancellationToken).ConfigureAwait(false) };
    }

    /// <summary>
    /// The 'open' of the instance's own needs-you row (q65): its 'park:' row (a root's park, or its deepest parked descendant's), else its 'desk:' row, else its
    /// 'takeover:' row; null when none waits on it. A row that can't be read is skipped (the detail is not refused for it), as the needs-you joins do.
    /// </summary>
    private async ValueTask<NeedsYouOpen?> OpenOf(InstanceAddress address, CancellationToken cancellationToken)
    {
        foreach (var id in new[] { NeedsYouRowIds.Park(address), NeedsYouRowIds.Desk(address), NeedsYouRowIds.Takeover(address) })
        {
            var record = await views.Get<NeedsYouRowRecord>(ViewNames.NeedsYou, id.Value, cancellationToken).ConfigureAwait(false);
            if (record.IsSuccess && record.Value is { } found)
            {
                return found.Row.Open;
            }
        }

        return null;
    }

    /// <summary>The title of the item the instance is on; its address when it has none or the view has no row.</summary>
    private async ValueTask<string> TitleOf(InstanceView view, CancellationToken cancellationToken)
    {
        if (view.ItemId is { } item)
        {
            var row = await views.Get<ItemRow>(ViewNames.Items, item.Value, cancellationToken).ConfigureAwait(false);
            if (row.IsSuccess && row.Value is { Title.Length: > 0 } found)
            {
                return found.Title;
            }
        }

        return view.Address.Value;
    }

    private async ValueTask<Result<ViewCounts>> Counts(ItemKey? item, CancellationToken cancellationToken)
    {
        if (item is not { } key)
        {
            return new ViewCounts(0, 0, 0, Context: null);
        }

        var timeline = await views.Range<TimelineRow>(ViewNames.ItemTimeline, ItemTimelineKeys.Of(key.Value, 0), ItemTimelineKeys.EndOf(key.Value), int.MaxValue, cancellationToken).ConfigureAwait(false);
        var decisions = await views.Where<HumanDecisionRow>(ViewNames.Decisions, ViewColumn.Item, key.Value, cancellationToken).ConfigureAwait(false);
        var requests = await views.Where<RequestRow>(ViewNames.Requests, ViewColumn.Item, key.Value, cancellationToken).ConfigureAwait(false);
        if (timeline.IsError || decisions.IsError || requests.IsError)
        {
            return timeline.IsError ? timeline.Error : decisions.IsError ? decisions.Error : requests.Error;
        }

        return new ViewCounts(timeline.Value.Count, decisions.Value.Count, requests.Value.Count, Context: null);
    }

    /// <summary>The parent's reference; null for a root, or when the parent can't be read now (a detail is not refused for it).</summary>
    private async ValueTask<ParentRef?> Parent(InstanceView view, CancellationToken cancellationToken)
    {
        if (view.ParentId is not { } id)
        {
            return null;
        }

        var address = await directory.Resolve(id.Value, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return null;
        }

        var parent = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        return parent.IsError ? null : new ParentRef(id, address.Value, await TitleOf(parent.Value, cancellationToken).ConfigureAwait(false), Task: null, Workstream: null);
    }
}
