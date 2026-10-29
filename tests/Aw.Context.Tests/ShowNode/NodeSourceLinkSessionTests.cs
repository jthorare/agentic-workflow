using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ShowNode;

/// <summary>A session gets a record's link only for a record its repo's profile allows or its scoped map holds (issue #1702).</summary>
public sealed class NodeSourceLinkSessionTests : IDisposable
{
    private const string Repo = "aw-split-panes";
    private const string Ticket = "linear:issue/0e5a1b00-0000-4000-8000-000000000a01";
    private const string LinearUrl = "https://linear.app/acme/issue/ENG-12/split-panes";

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope SessionOf(string team)
    {
        using var section = System.Text.Json.JsonDocument.Parse("""{"sessions":{"enabled":true},"sources":{"linear":{"teams":["TEAM"]}}}""".Replace("TEAM", team, StringComparison.Ordinal));
        return new(section.RootElement.Clone(), MapWorld.Repos, new ContextCaller(ContextCallerKind.Session, ContextRepoName.From(Repo), ContextClient.Http));
    }

    // The feature names the record in its evidence; the map holds no record node, so only the profile's audience can allow it.
    private async Task Setup()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", $"repo: {Repo}", "nodes:",
            $"  - {{key: \"feature:a\", label: \"Alpha\", evidence: [{{key: \"{Ticket}\"}}]}}",
            "  - {key: \"code:src/a.ts\", label: \"a.ts\", provenance: deterministic}",
            "edges:",
            "  - {from: \"code:src/a.ts\", to: \"feature:a\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/a.ts\"}]}"));
        var record = RecordSamples.Record(Ticket) with { Url = LinearUrl, Audience = AudienceKey.From("linear:ENG") };
        var committed = await _world.Records.Commit(RecordSamples.Batch(record.Source, 1, RecordSamples.Write(record, "text")), MapWorld.None);
        Assert.True(committed.IsSuccess, committed.IsError ? committed.Error.Message : null);
    }

    private async Task<string?> UrlFor(ContextScope scope) => (await _world.Engine.ShowNode("feature:a", scope, MapWorld.None)).Value.Node.Source?.Url;

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_gets_the_link_of_a_record_its_repo_s_audience_allows()
    {
        await Setup();

        Assert.Equal(LinearUrl, await UrlFor(SessionOf("ENG")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_gets_no_link_for_a_record_outside_its_audience()
    {
        await Setup();

        Assert.Null(await UrlFor(SessionOf("OPS")));
    }
}
