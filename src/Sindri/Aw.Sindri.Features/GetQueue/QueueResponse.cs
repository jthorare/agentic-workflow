using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetQueue;

/// <summary>The queue (API spec §3.6): one entry per group in QueueGroups.Order, even when empty (UI spec E-18).</summary>
/// <param name="Groups">The groups, in order; none when <paramref name="NotModified"/>.</param>
/// <param name="Empty">The queue's sentence when no group has an item (or the views are stale), else null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
/// <param name="Counts">The open items per group, set when the request has a limit (#2788).</param>
/// <param name="Tag">The answer's entity tag, built from the last items-view write, the projector's state, whether the views are behind and the caller's scope.</param>
/// <param name="NotModified">True when the request's IfNoneMatch equals <paramref name="Tag"/>: the answer has no body.</param>
public sealed record QueueResponse(IReadOnlyList<QueueGroupView> Groups, string? Empty, AsOf AsOf, QueueGroupCounts? Counts = null, string? Tag = null, bool NotModified = false);
