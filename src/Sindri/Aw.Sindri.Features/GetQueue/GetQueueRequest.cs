using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetQueue;

/// <summary>The queue: open items in the fixed groups (GET /sindri/queue; API spec §3.1, §3.6).</summary>
/// <param name="Limit">At most this many items per group; null keeps every open item (#2788).</param>
/// <param name="Group">Only this group: waitingOnYou, withYou, running or queued; null keeps every group.</param>
/// <param name="After">The cursor a group's previous page returned in Next; null starts at the top.</param>
/// <param name="IfNoneMatch">The Tag of an earlier answer; the same tag now answers NotModified with no body.</param>
public sealed record GetQueueRequest(int? Limit = null, string? Group = null, string? After = null, string? IfNoneMatch = null) : IQuery<QueueResponse>;
