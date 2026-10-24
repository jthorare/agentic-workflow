using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListDecisions;

/// <summary>
/// The decision list (API spec §3.1): the decisions, by pending-ness and
/// by item. The pending ones come oldest first, then the decided and voided ones newest first, at most 200 of those (D6, plan Q6).
/// </summary>
/// <param name="views">The 'decisions' view.</param>
/// <param name="items">Resolves the item filter.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class ListDecisionsService(IViewReader views, IItemResolver items, IProjectorStatus status, ICallerContextAccessor callers) : IService<ListDecisionsRequest, ListDecisionsResponse>
{
    private const int SettledMax = 200;

    public async ValueTask<Result<ListDecisionsResponse>> Handle(ListDecisionsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("the decision list needs a caller");
        }

        var current = status.Current;
        ItemId? item = null;
        if (request.Item is { } reference)
        {
            var resolved = await items.Resolve(reference, caller, cancellationToken).ConfigureAwait(false);
            if (resolved.IsError)
            {
                return resolved.Error;
            }

            item = resolved.Value.Id;
        }

        var rows = await views.List<HumanDecisionRow>(ViewNames.Decisions, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var views_ = new List<DecisionView>();
        foreach (var row in Ordered(rows.Value.Where(r => item is not { } named || r.Item == named), request.Pending))
        {
            var view = DecisionViews.From(row);
            if (view.IsError)
            {
                return view.Error;
            }

            views_.Add(view.Value);
        }

        return new ListDecisionsResponse(views_, EmptyText.For(current, views_.Count == 0, () => EmptyText.Decisions(request.Pending, item)), current.AsOf);
    }

    /// <summary>The pending rows oldest first, then the settled ones newest first (at most <see cref="SettledMax"/>), as <paramref name="pending"/> asks.</summary>
    private static IEnumerable<HumanDecisionRow> Ordered(IEnumerable<HumanDecisionRow> visible, bool? pending)
    {
        var rows = visible.ToList();
        var waiting = rows.Where(r => pending != false && r.State == DecisionState.Pending)
            .OrderBy(static r => r.FiledAt).ThenBy(static r => r.Id.Value, StringComparer.Ordinal);
        var settled = rows.Where(r => pending != true && r.State != DecisionState.Pending)
            .OrderByDescending(static r => r.DecidedAt ?? r.FiledAt).ThenByDescending(static r => r.Id.Value, StringComparer.Ordinal).Take(SettledMax);
        return waiting.Concat(settled);
    }
}
