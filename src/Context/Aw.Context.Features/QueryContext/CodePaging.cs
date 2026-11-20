using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.QueryContext;

/// <summary>Puts the ranked page of code files into an answer (#3673).</summary>
internal static class CodePaging
{
    internal static ContextResult WithCodePage(this ContextResult result, ContextQuery query)
    {
        if (result.Sections.FirstOrDefault(s => s.Name == "code") is not { } code)
        {
            return result;
        }

        var page = CodeRanker.Rank(code.Items, query.Text ?? query.Symbol, query.Symbol, query.Window?.Offset ?? 0, query.Window?.Limit ?? CodeRanker.DefaultLimit);
        var sections = result.Sections.Select(s => s.Name == "code" ? s with { Items = page.Items } : s).ToList();
        return result with { Sections = sections, CodeMatched = page.Total, NextCursor = page.NextCursor };
    }
}
