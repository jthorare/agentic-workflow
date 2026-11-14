using Aw.Context.Core.Results;

namespace Aw.Context.Features.QueryContext;

/// <summary>Scores the answer's code items and returns one page of them (#3673).</summary>
internal static class CodeRanker
{
    internal const int DefaultLimit = 25;

    internal static RankedPage Rank(IReadOnlyList<ResultItem> items, string? text, string? symbol, int offset, int limit)
    {
        _ = (text, symbol, offset, limit);   // red stub: the ranking lands in the next commit
        return new RankedPage(items, items.Count, NextCursor: null);
    }
}
