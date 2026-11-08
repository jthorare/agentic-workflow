#pragma warning disable MA0182 // why: the graph block registry is not registered in DI yet (EmptyGraphBlockCatalog); remove this when it is
using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.News;

/// <summary>Hacker News through its Firebase API. Reads go through <see cref="SafeHttp"/>: allowlisted hosts only, no cross-host redirect, no loopback, 1 MB cap.</summary>
/// <param name="http">Sends the requests (built with redirects off).</param>
/// <param name="allowedHosts">The hosts it may call.</param>
internal sealed class HackerNewsProvider(HttpClient http, IReadOnlySet<string> allowedHosts) : INewsProvider
{
    private const string Origin = "https://hacker-news.firebaseio.com/v0/";
    private const string Source = "news.ycombinator.com";
    private const int BatchSize = 10;

    private static readonly Dictionary<string, string> _lists = new(StringComparer.Ordinal)
    {
        ["top"] = "topstories",
        ["new"] = "newstories",
        ["best"] = "beststories",
        ["ask"] = "askstories",
        ["show"] = "showstories",
    };

    /// <inheritdoc />
    public string Name => "hackernews";

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<NewsItem>>> Read(NewsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_lists.TryGetValue(request.Feed, out var list))
        {
            return SindriErrors.GraphBlockFailed.With($"Hacker News has no feed '{request.Feed}'; use one of: {string.Join(", ", _lists.Keys)}");
        }

        var ids = await Ids(list, cancellationToken).ConfigureAwait(false);
        if (ids.IsError)
        {
            return ids.Error;
        }

        var kept = new List<NewsItem>();
        for (var start = 0; start < ids.Value.Count && kept.Count < request.Limit; start += BatchSize)
        {
            var batch = await Task.WhenAll(ids.Value.Skip(start).Take(BatchSize).Select(id => Item(id, cancellationToken))).ConfigureAwait(false);
            if (batch.FirstOrDefault(static b => b.IsError) is { IsError: true } failed)
            {
                return failed.Error;
            }

            kept.AddRange(batch.Select(static b => b.Value).OfType<NewsItem>().Where(story => NewsFilter.Keeps(story, request)).Take(request.Limit - kept.Count));
        }

        return kept;
    }

    private async ValueTask<Result<IReadOnlyList<long>>> Ids(string list, CancellationToken cancellationToken)
    {
        var body = await SafeHttp.Send(http, allowedHosts, new SafeCall(HttpMethod.Get, new Uri($"{Origin}{list}.json")), cancellationToken).ConfigureAwait(false);
        if (body.IsError)
        {
            return body.Error;
        }

        using var doc = JsonDocument.Parse(body.Value);
        IReadOnlyList<long> ids = doc.RootElement.ValueKind == JsonValueKind.Array
            ? [.. doc.RootElement.EnumerateArray().Where(static e => e.TryGetInt64(out _)).Select(static e => e.GetInt64())]
            : [];
        return Results.Success(ids);
    }

    private async Task<Result<NewsItem?>> Item(long id, CancellationToken cancellationToken)
    {
        var key = id.ToString(CultureInfo.InvariantCulture);
        var body = await SafeHttp.Send(http, allowedHosts, new SafeCall(HttpMethod.Get, new Uri($"{Origin}item/{key}.json")), cancellationToken).ConfigureAwait(false);
        if (body.IsError)
        {
            return body.Error;
        }

        using var doc = JsonDocument.Parse(body.Value);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Flag(root, "deleted") || Flag(root, "dead") || Text(root, "title") is not { } title)
        {
            return Results.Success<NewsItem?>(value: null);
        }

        return Results.Success<NewsItem?>(new NewsItem(
            Key: key,
            Title: title,
            Url: Text(root, "url") ?? $"https://{Source}/item?id={key}",
            Source: Source,
            Score: Number(root, "score"),
            Comments: Number(root, "descendants"),
            PublishedAt: root.TryGetProperty("time", out var time) && time.TryGetInt64(out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null));
    }

    private static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Number(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : null;

    private static bool Flag(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
