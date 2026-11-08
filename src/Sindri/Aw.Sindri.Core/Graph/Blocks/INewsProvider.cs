using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where a 'news' node's items come from: Hacker News, an RSS feed. One class per source; its hosts are on the profile's allowlist.</summary>
public interface INewsProvider
{
    /// <summary>The name a node's 'provider' uses, such as 'hackernews'.</summary>
    public string Name { get; }

    /// <summary>Reads the items, newest feed order first.</summary>
    /// <param name="request">What to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The items, or why they could not be read (a host off the allowlist, a redirect to another host, a failed call).</returns>
    public ValueTask<Result<IReadOnlyList<NewsItem>>> Read(NewsRequest request, CancellationToken cancellationToken);
}
