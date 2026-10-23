using System.Net;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.WholeMap;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'sindri context map whole' over a scripted API: what it sends, what it prints and the exit codes (0 ok, 1 attention, 2 usage).</summary>
public sealed class ContextWholeVerbTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<int> Run(ScriptedApi api, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        return await new ContextWholeVerb(client, new Output(_out, _error, SliceHarness.Json)).Run(args, Token);
    }

    private static WholeMapView View()
    {
        var kinds = new Dictionary<string, int>(StringComparer.Ordinal) { ["feature"] = 2, ["code"] = 3 };
        static WholeMapArea Area(string key, string label, int members, bool loose) =>
            new(key, label, Trust.Trusted, members, new Dictionary<string, int>(StringComparer.Ordinal) { ["feature"] = 1, ["code"] = members - 1 }, loose);

        return new WholeMapView(
            1, "v12", Draft: false, DateTimeOffset.Parse("2026-10-23T06:00:00Z", System.Globalization.CultureInfo.InvariantCulture), "my-app", WholeMapDetail.Areas,
            new WholeMapCounts(5, 4, kinds), [Area("area:unassigned", "Not linked to a feature yet", 1, loose: true), Area("feature:billing", "Billing", 4, loose: false)],
            [new WholeMapAreaEdge("feature:billing", "area:unassigned", EdgeType.Mentions, Count: 1, Confirmed: 1)], Truncated: false)
        {
            MapCommit = "9c2e4f1",
            Fallback = "over-budget",
            Located = [new WholeMapLocated("feature:billing", "feature:billing"), new WholeMapLocated("feature:nope", Area: null)],
        };
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Whole_sends_every_flag_escaped_with_locate_repeated()
    {
        var api = new ScriptedApi((HttpStatusCode.NotFound, """{"type":"x","title":"SND-CONTEXT-200","status":404,"detail":"no such area"}"""));

        var exit = await Run(api, "--detail", "nodes", "--area", "feature:a b", "--locate", "feature:x", "--locate", "code:y.ts", "--map-version", "v3", "--repo", "my-app");

        Assert.Equal("/sindri/context/map/whole?detail=nodes&area=feature%3Aa%20b&locate=feature%3Ax&locate=code%3Ay.ts&mapVersion=v3&repo=my-app", api.Calls[0].Route);
        Assert.Equal(1, exit);   // 200 is attention
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Whole_with_no_flags_asks_for_the_bare_route_and_a_draft_asks_for_the_draft()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(View(), SliceHarness.Json)), (HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(View(), SliceHarness.Json)));

        var bare = await Run(api);
        var draft = await Run(api, "--draft");

        Assert.Equal(("/sindri/context/map/whole", "/sindri/context/map/whole?draft=true", 0, 0), (api.Calls[0].Route, api.Calls[1].Route, bare, draft));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Whole_draft_with_a_map_version_is_006_and_sends_nothing()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, "{}"));

        var exit = await Run(api, "--draft", "--map-version", "v1");

        Assert.Equal(1, exit);
        Assert.Empty(api.Calls);
        Assert.Contains("SND-CONTEXT-006", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--nope")]
    [InlineData("--detail")]
    [InlineData("word")]
    public async Task Whole_with_a_bad_flag_or_a_word_is_a_usage_error_sent_nowhere(string arg)
    {
        var api = new ScriptedApi((HttpStatusCode.OK, "{}"));

        var exit = await Run(api, arg);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Whole_prints_the_counts_line_and_one_line_per_area_biggest_first_then_where_the_keys_are()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(View(), SliceHarness.Json)));

        var exit = await Run(api, "--locate", "feature:billing");

        var lines = _out.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(exit == 0, _error.ToString());
        Assert.Equal("whole map v12@9c2e4f1 · 5 nodes, 4 edges · detail: areas (over-budget)", lines[0]);
        Assert.Equal("  Billing · 4 members · 1 feature, 3 code", lines[2]);
        Assert.Equal("  Not linked to a feature yet · 1 member · 1 feature, 0 code", lines[3]);
        Assert.Contains("  feature:nope → not in the map", lines, StringComparer.Ordinal);
        Assert.DoesNotContain('\u001b', _out.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Whole_json_prints_the_body_as_served()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(View(), SliceHarness.Json)));

        var exit = await Run(api, "--json");

        Assert.True(exit == 0, _error.ToString());
        Assert.Contains("\"schemaVersion\":1", _out.ToString(), StringComparison.Ordinal);
    }
}
