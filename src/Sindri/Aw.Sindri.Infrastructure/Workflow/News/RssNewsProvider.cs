#pragma warning disable MA0182 // why: the graph block registry is not registered in DI yet (EmptyGraphBlockCatalog); remove this when it is
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.News;

/// <summary>Any RSS or Atom feed whose host is on the allowlist; same limits as <see cref="HackerNewsProvider"/>. The feed is the request's 'feed' URL.</summary>
/// <param name="http">Sends the requests (built with redirects off).</param>
/// <param name="allowedHosts">The hosts it may call.</param>
internal sealed class RssNewsProvider(HttpClient http, IReadOnlySet<string> allowedHosts) : INewsProvider
{
    /// <inheritdoc />
    public string Name => "rss";

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<NewsItem>>> Read(NewsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Uri.TryCreate(request.Feed, UriKind.Absolute, out var uri))
        {
            return SindriErrors.GraphBlockFailed.With($"'{request.Feed}' is not a feed URL");
        }

        var body = await SafeHttp.Send(http, allowedHosts, new SafeCall(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        if (body.IsError)
        {
            return body.Error;
        }

        try
        {
            using var reader = XmlReader.Create(new StringReader(body.Value), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var doc = XDocument.Load(reader);
            return doc.Descendants()
                .Where(static e => string.Equals(e.Name.LocalName, "item", StringComparison.Ordinal) || string.Equals(e.Name.LocalName, "entry", StringComparison.Ordinal))
                .Select(e => Item(e, uri.Host))
                .Where(item => NewsFilter.Keeps(item, request))
                .Take(request.Limit)
                .ToList();
        }
        catch (XmlException ex)
        {
            return SindriErrors.GraphBlockFailed.With($"{uri.Host} did not send a feed ({ex.Message})");
        }
    }

    private static NewsItem Item(XElement e, string host)
    {
        var title = Child(e, "title") ?? string.Empty;
        var link = Child(e, "link") ?? e.Elements().FirstOrDefault(static c => string.Equals(c.Name.LocalName, "link", StringComparison.Ordinal))?.Attribute("href")?.Value;
        return new NewsItem(
            Key: Child(e, "guid") ?? Child(e, "id") ?? link ?? title,
            Title: title.Trim(),
            Url: link,
            Source: host,
            Score: null,
            Comments: null,
            PublishedAt: Moment(Child(e, "pubDate") ?? Child(e, "published") ?? Child(e, "updated")));
    }

    private static DateTimeOffset? Moment(string? text) =>
        text is not null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : null;

    private static string? Child(XElement e, string name)
    {
        var value = e.Elements().FirstOrDefault(c => string.Equals(c.Name.LocalName, name, StringComparison.Ordinal))?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
