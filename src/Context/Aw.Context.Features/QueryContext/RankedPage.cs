using Aw.Context.Core.Results;

namespace Aw.Context.Features.QueryContext;

/// <summary>One page of ranked code items: the items, how many there were in all, and the cursor of the next page (null on the last).</summary>
/// <param name="Items">The page, best first, each with a score and a reason.</param>
/// <param name="Total">Every ranked item, before paging.</param>
/// <param name="NextCursor">The offset of the next page as text, or null when this is the last page.</param>
internal sealed record RankedPage(IReadOnlyList<ResultItem> Items, int Total, string? NextCursor);
