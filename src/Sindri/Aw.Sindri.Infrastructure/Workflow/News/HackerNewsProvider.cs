#pragma warning disable MA0182 // why: red stub; the green commit registers it
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.News;

/// <summary>Hacker News through its Firebase API. Only hosts on the allowlist are called; no redirect to another host is followed; loopback is refused; a body past 1 MB is refused.</summary>
/// <param name="http">Sends the requests.</param>
/// <param name="allowedHosts">The hosts it may call.</param>
internal sealed class HackerNewsProvider(HttpClient http, IReadOnlySet<string> allowedHosts) : INewsProvider
{
    /// <inheritdoc />
    public string Name => "hackernews";

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<NewsItem>>> Read(NewsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(http.GetType().Name + allowedHosts.GetType().Name);
}
