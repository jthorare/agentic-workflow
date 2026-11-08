using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.Workflow.News;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class NewsBlockTests
{
    private static readonly IReadOnlySet<string> _hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "hacker-news.firebaseio.com", "feeds.example.com" };

    [Fact]
    public async Task Hacker_News_top_returns_items_in_feed_order_and_honors_limit_and_since()
    {
        // 1793491200 is 2026-11-01T00:00:00Z; item 10 is older than that and drops out before the limit counts.
        var stub = new StubHttp((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/v0/topstories.json" => StubHttp.Json("[10,11,12,13]"),
            "/v0/item/10.json" => StubHttp.Json("""{"id":10,"title":"Old","url":"https://a.example/old","score":5,"descendants":1,"time":1793404800,"type":"story"}"""),
            "/v0/item/11.json" => StubHttp.Json("""{"id":11,"title":"Eleven","url":"https://b.example/11","score":120,"descendants":45,"time":1793491300,"type":"story"}"""),
            "/v0/item/12.json" => StubHttp.Json("""{"id":12,"title":"Ask HN: twelve","score":30,"descendants":9,"time":1793491400,"type":"story"}"""),
            "/v0/item/13.json" => StubHttp.Json("""{"id":13,"title":"Thirteen","url":"https://c.example/13","score":7,"descendants":0,"time":1793491500,"type":"story"}"""),
            _ => StubHttp.Json("{}", System.Net.HttpStatusCode.NotFound),
        });
        var block = new NewsBlock([new HackerNewsProvider(stub.Client(), _hosts)]);
        var config = new JsonObject { ["provider"] = "hackernews", ["feed"] = "top", ["limit"] = 2, ["since"] = "2026-11-01T00:00:00Z" };

        var result = await block.Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var items = result.Value.Outputs["items"]!.AsArray();
        Assert.Equal("11,12", string.Join(',', items.Select(i => i!["id"]!.GetValue<string>())));
        Assert.Equal(2, result.Value.Outputs["count"]!.GetValue<int>());
        Assert.Equal("Eleven", items[0]!["title"]!.GetValue<string>());
        Assert.Equal("https://b.example/11", items[0]!["url"]!.GetValue<string>());
        Assert.Equal("news.ycombinator.com", items[0]!["source"]!.GetValue<string>());
        Assert.Equal(120, items[0]!["score"]!.GetValue<int>());
        Assert.Equal(45, items[0]!["comments"]!.GetValue<int>());
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T00:01:40Z", System.Globalization.CultureInfo.InvariantCulture), DateTimeOffset.Parse(items[0]!["publishedAt"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_host_off_the_allowlist_and_a_cross_host_redirect_are_refused()
    {
        var stub = new StubHttp((request, _) => request.RequestUri!.Host == "feeds.example.com"
            ? StubHttp.Redirect("https://evil.example.net/rss")
            : StubHttp.Json("<rss/>"));
        var block = new NewsBlock([new RssNewsProvider(stub.Client(), _hosts)]);

        var offList = await block.Execute(BlockFakes.Context(new JsonObject { ["provider"] = "rss", ["feed"] = "https://evil.example.net/rss" }), TestContext.Current.CancellationToken);
        var loopback = await block.Execute(BlockFakes.Context(new JsonObject { ["provider"] = "rss", ["feed"] = "http://127.0.0.1:9/rss" }), TestContext.Current.CancellationToken);
        Assert.Empty(stub.Calls);

        var redirected = await block.Execute(BlockFakes.Context(new JsonObject { ["provider"] = "rss", ["feed"] = "https://feeds.example.com/rss" }), TestContext.Current.CancellationToken);

        Assert.True(offList.IsError);
        Assert.Equal(SindriErrors.GraphBlockFailed.Code, offList.Error.Code);
        Assert.Contains("evil.example.net", offList.Error.Message, StringComparison.Ordinal);
        Assert.Equal(SindriErrors.GraphBlockFailed.Code, loopback.Error.Code);
        Assert.True(redirected.IsError);
        Assert.Equal(SindriErrors.GraphBlockFailed.Code, redirected.Error.Code);
        Assert.Contains("evil.example.net", redirected.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(stub.Calls, call => call.Uri.Host == "evil.example.net");
    }
}
