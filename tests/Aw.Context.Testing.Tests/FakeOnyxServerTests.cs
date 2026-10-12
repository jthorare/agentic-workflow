using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Fakes;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Tests;

/// <summary>The fake Onyx HTTP server replays the recorded documents on loopback (spec §5.2, §10, §12, §14 S-L2).</summary>
[Lane(TestLane.Integration)]
public sealed class FakeOnyxServerTests
{
    private const string Spl105 = "0e5a1b00-0000-4000-8000-000000000105";

    [Fact]
    public async Task Search_ranks_the_matching_issue_first_with_its_uuid_identifier_and_updated_at()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var docs = await Search(onyx, new JsonObject { ["message"] = "minimum pane width" });

        var top = docs[0]!;
        Assert.Equal(Spl105, (string?)top["document_id"]);
        Assert.Equal("SPL-105", (string?)top["metadata"]!["identifier"]);
        Assert.Equal("linear", (string?)top["source_type"]);
        Assert.Equal(DateTimeOffset.Parse("2026-08-12T15:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse((string)top["updated_at"]!, CultureInfo.InvariantCulture));
        Assert.True((double)top["score"]! > 0);
        Assert.Equal(docs.Count, docs.Select(d => (string?)d!["document_id"]).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Search_filters_by_source_type_and_time_cutoff_and_honors_limit()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var slack = await Search(onyx, Request("resize step", ["slack"], cutoff: null, limit: null));
        var recent = await Search(onyx, Request("pane", sources: null, "2026-07-29T00:00:00Z", limit: null));
        var one = await Search(onyx, Request("pane", sources: null, cutoff: null, 1));

        Assert.Equal(["C0FIXTURE01__1784972400.000100"], slack.Select(d => (string?)d!["document_id"]), StringComparer.Ordinal);
        Assert.All(recent, d => Assert.True(DateTimeOffset.Parse((string)d!["updated_at"]!, CultureInfo.InvariantCulture) >= new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero)));
        Assert.Contains(recent, d => (string?)d!["document_id"] == Spl105);
        _ = Assert.Single(one);
    }

    [Fact]
    public async Task Chunk_info_returns_a_documents_text_and_404s_an_unknown_one()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (Status, Body) = await Http.Get(new Uri(onyx.Url, $"api/document/chunk-info?document_id={Spl105}&chunk_id=3"), onyx.Authorization);
        var missing = await Http.Get(new Uri(onyx.Url, "api/document/chunk-info?document_id=nope&chunk_id=0"), onyx.Authorization);

        Assert.Equal(HttpStatusCode.OK, Status);
        Assert.Contains("20 columns", (string?)Body!["content"], StringComparison.Ordinal);
        Assert.True((int)Body!["num_tokens"]! > 0);
        Assert.Equal(HttpStatusCode.NotFound, missing.Status);
        Assert.Contains("nope", (string?)missing.Body!["detail"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_route_or_an_unsupported_field_is_a_clear_error()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (Status, Body) = await Http.Post(new Uri(onyx.Url, "api/chat/send-message"), onyx.Authorization, "{}");
        var field = await Http.Post(new Uri(onyx.Url, "api/query/document-search"), onyx.Authorization,
            new JsonObject { ["message"] = "pane", ["retrieval_options"] = new JsonObject { ["filters"] = new JsonObject { ["tags"] = new JsonArray("x") } } }.ToJsonString());
        var noMessage = await Http.Post(new Uri(onyx.Url, "api/query/document-search"), onyx.Authorization, "{}");

        Assert.Equal(HttpStatusCode.NotFound, Status);
        Assert.Contains("api/query/document-search", (string?)Body!["detail"], StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, field.Status);
        Assert.Contains("tags", field.Body!["detail"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noMessage.Status);
        Assert.Contains("message", noMessage.Body!["detail"]!.ToJsonString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"message\":\"pane\",\"search_type\":\"keyword\"}", "search_type")]
    [InlineData("{\"message\":\"pane\",\"retrieval_options\":{\"enable_auto_detect_filters\":false}}", "enable_auto_detect_filters")]
    [InlineData("{\"message\":\"pane\",\"retrieval_options\":{\"filters\":{\"source_type\":\"linear\"}}}", "source_type")]
    [InlineData("{\"message\":\"pane\",\"retrieval_options\":{\"filters\":{\"time_cutoff\":5}}}", "time_cutoff")]
    [InlineData("{\"message\":\"pane\",\"retrieval_options\":{\"limit\":1.5}}", "limit")]
    [InlineData("{\"message\":\"pane\",\"retrieval_options\":{\"offset\":-1}}", "offset")]
    public async Task A_field_the_fake_does_not_read_or_a_badly_typed_value_is_a_422_naming_it(string body, string named)
    {
        await using var onyx = await FakeOnyxServer.Start();

        var (status, reply) = await Http.Post(new Uri(onyx.Url, "api/query/document-search"), onyx.Authorization, body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Contains(named, reply!["detail"]!.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Offset_skips_into_the_ranked_list()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var all = await Search(onyx, Request("pane", sources: null, cutoff: null, limit: null));
        var second = await Search(onyx, new JsonObject { ["message"] = "pane", ["retrieval_options"] = new JsonObject { ["offset"] = 1, ["limit"] = 1 } });

        Assert.Equal((string?)all[1]!["document_id"], (string?)Assert.Single(second)!["document_id"]);
    }

    [Fact]
    public async Task An_empty_source_type_list_is_no_filter_as_in_onyx()
    {
        await using var onyx = await FakeOnyxServer.Start();

        var unfiltered = await Search(onyx, Request("pane", sources: null, cutoff: null, limit: null));
        var empty = await Search(onyx, Request("pane", [], cutoff: null, limit: null));

        Assert.Equal(unfiltered.Select(d => (string?)d!["document_id"]), empty.Select(d => (string?)d!["document_id"]), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_missing_or_wrong_bearer_token_is_401()
    {
        await using var onyx = await FakeOnyxServer.Start();
        var search = new Uri(onyx.Url, "api/query/document-search");

        var (Status, Body) = await Http.Post(search, authorization: null, "{\"message\":\"pane\"}");
        var raw = await Http.Post(search, onyx.Token, "{\"message\":\"pane\"}");

        Assert.Equal(HttpStatusCode.Unauthorized, Status);
        Assert.Equal(HttpStatusCode.Unauthorized, raw.Status);
    }

    [Fact]
    public async Task It_listens_on_loopback_and_never_logs_the_authorization_header()
    {
        const string planted = "planted-onyx-91c2";
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { Token = planted });

        _ = await Search(onyx, new JsonObject { ["message"] = "pane" });
        _ = await Http.Get(new Uri(onyx.Url, "api/nothing"), "Bearer " + planted);
        _ = await Http.Post(new Uri(onyx.Url, "api/query/document-search"), "Bearer wrong-" + planted, "{\"message\":\"pane\"}");

        Assert.Equal("127.0.0.1", onyx.Url.Host);
        Assert.Equal(3, onyx.Requests.Count);
        Assert.NotEmpty(onyx.Log);
        Assert.DoesNotContain(onyx.Log, line => line.Contains(planted, StringComparison.Ordinal));
        Assert.DoesNotContain(onyx.Requests, r => r.ToString().Contains(planted, StringComparison.Ordinal));
    }

    private static JsonObject Request(string message, string[]? sources, string? cutoff, int? limit)
    {
        var filters = new JsonObject();
        if (sources is not null)
        {
            filters["source_type"] = new JsonArray([.. sources.Select(s => JsonValue.Create(s))]);
        }

        if (cutoff is not null)
        {
            filters["time_cutoff"] = cutoff;
        }

        var options = new JsonObject { ["filters"] = filters };
        if (limit is not null)
        {
            options["limit"] = limit;
        }

        return new JsonObject { ["message"] = message, ["retrieval_options"] = options };
    }

    private static async Task<JsonArray> Search(FakeOnyxServer onyx, JsonObject request)
    {
        var (status, body) = await Http.Post(new Uri(onyx.Url, "api/query/document-search"), onyx.Authorization, request.ToJsonString());
        Assert.Equal(HttpStatusCode.OK, status);
        return body!["top_documents"]!.AsArray();
    }
}
