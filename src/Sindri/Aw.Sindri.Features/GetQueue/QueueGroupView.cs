using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Features.GetQueue;

/// <summary>One queue group: '{group, items, empty, next}' (U2-API-2).</summary>
/// <param name="Group">The group.</param>
/// <param name="Items">Its items, the oldest 'since' first.</param>
/// <param name="Empty">The group's own sentence when it has no item, else null.</param>
/// <param name="Next">The cursor for this group's next page, null on the last page or without a limit (#2788).</param>
public sealed record QueueGroupView(QueueGroup Group, IReadOnlyList<ItemSummary> Items, string? Empty, string? Next = null);
