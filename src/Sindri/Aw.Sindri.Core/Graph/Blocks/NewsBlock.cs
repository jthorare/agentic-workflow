using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'news' block: reads Hacker News or an RSS feed through an <see cref="INewsProvider"/>.</summary>
public sealed class NewsBlock(IEnumerable<INewsProvider> providers) : IGraphBlock
{
    private const int DefaultLimit = 30;
    private const int MaxLimit = 200;
    private const string DefaultProvider = "hackernews";

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["items"] = "array", ["count"] = "number" });

    private readonly List<INewsProvider> _providers = [.. providers];

    /// <inheritdoc />
    public string Type => "news";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new(
    [
        new("provider", BlockFieldType.Text, Required: false),
        new("feed", BlockFieldType.Text, Required: false),
        new("query", BlockFieldType.Text, Required: false),
        new("limit", BlockFieldType.Number, Required: false),
        new("since", BlockFieldType.Text, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"provider":"hackernews","feed":"top","query":"sqlite","limit":10,"since":"{{trigger.scheduledFor}}"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var problems = new List<(string Field, string Problem)>();
        if (config["limit"] is { } limit && BlockConfig.Whole(limit) is { } n && n is < 1 or > MaxLimit)
        {
            problems.Add(("limit", $"must be between 1 and {MaxLimit.ToString(CultureInfo.InvariantCulture)}; change it"));
        }

        if (BlockConfig.Text(config["since"]) is { } since && !since.Contains("{{", StringComparison.Ordinal) && BlockConfig.Moment(config["since"]) is null)
        {
            problems.Add(("since", "must be a date and time such as 2026-11-01T00:00:00Z; change it"));
        }

        return problems;
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var asked = Ask(context.Config);
        if (asked.IsError)
        {
            return asked.Error;
        }

        var (provider, request) = asked.Value;
        var read = await provider.Read(request, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var items = new JsonArray([.. read.Value.Select(Item)]);
        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["items"] = items, ["count"] = items.Count });
    }

    private Result<(INewsProvider Provider, NewsRequest Request)> Ask(JsonObject config)
    {
        var name = BlockConfig.Text(config["provider"]) ?? DefaultProvider;
        if (_providers.Find(p => string.Equals(p.Name, name, StringComparison.Ordinal)) is not { } provider)
        {
            return SindriErrors.GraphBlockFailed.With($"news has no provider '{name}'; use one of: {string.Join(", ", _providers.Select(static p => p.Name))}");
        }

        var feed = BlockConfig.Text(config["feed"]) ?? (string.Equals(name, DefaultProvider, StringComparison.Ordinal) ? "top" : null);
        var limit = BlockConfig.Whole(config["limit"]) ?? DefaultLimit;
        if (feed is null || limit is < 1 or > MaxLimit || (config["since"] is not null && BlockConfig.Moment(config["since"]) is null))
        {
            return SindriErrors.GraphBlockFailed.With($"news needs a 'feed' for provider '{name}', a 'limit' from 1 to {MaxLimit.ToString(CultureInfo.InvariantCulture)} and a 'since' that is a date and time");
        }

        return (provider, new NewsRequest(feed, BlockConfig.Text(config["query"]), limit, BlockConfig.Moment(config["since"])));
    }

    private static JsonNode Item(NewsItem item)
    {
        var json = new JsonObject { ["id"] = item.Key, ["title"] = item.Title, ["source"] = item.Source };
        if (item.Url is not null)
        {
            json["url"] = item.Url;
        }

        if (item.Score is { } score)
        {
            json["score"] = score;
        }

        if (item.Comments is { } comments)
        {
            json["comments"] = comments;
        }

        if (item.PublishedAt is { } at)
        {
            json["publishedAt"] = at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        return json;
    }
}
