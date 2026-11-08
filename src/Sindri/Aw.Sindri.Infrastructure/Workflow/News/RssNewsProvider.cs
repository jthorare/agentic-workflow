#pragma warning disable MA0182 // why: red stub; the green commit registers it
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.News;

/// <summary>Any RSS or Atom feed whose host is on the allowlist; same limits as <see cref="HackerNewsProvider"/>.</summary>
/// <param name="http">Sends the requests.</param>
/// <param name="allowedHosts">The hosts it may call.</param>
internal sealed class RssNewsProvider(HttpClient http, IReadOnlySet<string> allowedHosts) : INewsProvider
{
    /// <inheritdoc />
    public string Name => "rss";

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<NewsItem>>> Read(NewsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(http.GetType().Name + allowedHosts.GetType().Name);
}
