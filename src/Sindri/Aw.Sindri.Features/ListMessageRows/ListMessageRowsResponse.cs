using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.ListMessageRows;

/// <summary>A list's rows (messages spec §8.2).</summary>
/// <param name="List">The list.</param>
/// <param name="Rows">The rows, in order.</param>
/// <param name="Count">How many rows the whole list holds.</param>
/// <param name="Next">The key to pass as 'after' for the next page, or null at the end.</param>
public sealed record ListMessageRowsResponse(MessageList List, IReadOnlyList<ThreadRow> Rows, int Count, string? Next);
