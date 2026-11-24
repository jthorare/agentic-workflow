using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.ListMessageRows;

/// <summary>The rows of one list in its current order (GET /sindri/messages/lists/{list}?after=&amp;limit=, 'sindri messages list'; messages spec §8.2). A session gets keys, kinds and counts only (W17).</summary>
/// <param name="List">The list.</param>
/// <param name="After">The row key to start after, or null for the top.</param>
/// <param name="Limit">At most this many rows.</param>
public sealed record ListMessageRowsRequest(MessageList List, string? After, int Limit) : IQuery<ListMessageRowsResponse>;
