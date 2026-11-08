using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.News;

/// <summary>What every news provider keeps: the query and the 'since' time.</summary>
internal static class NewsFilter
{
    /// <summary>True when the item passes the request's query and since.</summary>
    /// <param name="item">The item.</param>
    /// <param name="request">The request.</param>
    /// <returns>Whether to keep it.</returns>
    public static bool Keeps(NewsItem item, NewsRequest request) =>
        (request.Query is null || item.Title.Contains(request.Query, StringComparison.OrdinalIgnoreCase))
        && (request.Since is not { } since || (item.PublishedAt is { } at && at >= since));
}
