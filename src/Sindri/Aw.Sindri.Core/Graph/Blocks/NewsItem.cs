namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One story or post a 'news' node reads. Its text comes from the web: untrusted wherever it reaches a prompt.</summary>
/// <param name="Key">The provider's key for it; the output field is called id.</param>
/// <param name="Title">The headline.</param>
/// <param name="Url">The link, or null for a post with none.</param>
/// <param name="Source">The site it came from, such as 'news.ycombinator.com'.</param>
/// <param name="Score">The points, or null when the source has none.</param>
/// <param name="Comments">The comment count, or null when the source has none.</param>
/// <param name="PublishedAt">When it was posted, or null when the source gives no time.</param>
public sealed record NewsItem(string Key, string Title, string? Url, string Source, int? Score, int? Comments, DateTimeOffset? PublishedAt);
