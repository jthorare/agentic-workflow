using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetQueue;

/// <summary>
/// The queue (API spec §3.6): the items, open ones only, in one entry per group of <see cref="QueueGroups.Order"/> (an empty
/// group keeps its entry and its sentence, E-18; stale views replace every sentence, the groups' too, and keep the rows), the oldest 'since' first in each. The top-level sentence is the stale-views one, else the queue's.
/// </summary>
/// <param name="views">The 'items' view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class GetQueueService(IViewReader views, IProjectorStatus status, ICallerContextAccessor callers) : IService<GetQueueRequest, QueueResponse>
{
    public async ValueTask<Result<QueueResponse>> Handle(GetQueueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the queue needs a caller");
        }

        var current = status.Current;
        var rows = await views.List<ItemRow>(ViewNames.Items, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var open = ItemSummaries.Visible(rows.Value, static r => QueueGroups.GroupOf(r.State) is not null);
        var groups = QueueGroups.Order.Select(group =>
        {
            var items = open.Where(i => QueueGroups.GroupOf(i.State) == group).ToList();
            return new QueueGroupView(group, items, EmptyText.For(current, items.Count == 0, () => EmptyText.Group(group)));
        }).ToList();
        return new QueueResponse(groups, EmptyText.For(current, open.Count == 0, EmptyText.Queue), current.AsOf);
    }
}
