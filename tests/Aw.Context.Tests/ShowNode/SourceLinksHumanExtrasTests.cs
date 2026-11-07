using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ShowNode;

/// <summary>
/// The source-links read gives a human every audience <see cref="RecordMask"/> gives, including <see cref="ProfileAudiencePolicy.HumanExtras"/>, and a session none of them (#2177).
/// The record under test has 'owner:library' as its only audience; the control record has the audience of the session's own team.
/// </summary>
public sealed class SourceLinksHumanExtrasTests : IDisposable
{
    private const string Repo = "aw-split-panes";
    private const string Ticket = "linear:issue/0e5a1b00-0000-4000-8000-000000000a01";
    private const string LinearUrl = "https://linear.app/acme/issue/ENG-12/split-panes";

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Session()
    {
        using var section = System.Text.Json.JsonDocument.Parse("""{"sessions":{"enabled":true},"sources":{"linear":{"teams":["ENG"]}}}""");
        return new(section.RootElement.Clone(), MapWorld.Repos, new ContextCaller(ContextCallerKind.Session, ContextRepoName.From(Repo), ContextClient.Http));
    }

    private async Task Setup(string audience)
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", $"repo: {Repo}", "nodes:",
            $"  - {{key: \"feature:a\", label: \"Alpha\", evidence: [{{key: \"{Ticket}\"}}]}}",
            "  - {key: \"code:src/a.ts\", label: \"a.ts\", provenance: deterministic}",
            "edges:",
            "  - {from: \"code:src/a.ts\", to: \"feature:a\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/a.ts\"}]}"));
        var record = RecordSamples.Record(Ticket) with { Url = LinearUrl, Audience = AudienceKey.From(audience) };
        var committed = await _world.Records.Commit(RecordSamples.Batch(record.Source, 1, RecordSamples.Write(record, "text")), MapWorld.None);
        Assert.True(committed.IsSuccess, committed.IsError ? committed.Error.Message : null);
    }

    private async Task<string?> UrlFor(ContextScope scope) => (await _world.Engine.ShowNode("feature:a", scope, MapWorld.None)).Value.Node.Source?.Url;

    [Fact, Trait("Lane", "unit")]
    public async Task A_human_gets_the_link_of_a_record_whose_only_audience_is_a_human_extra()
    {
        await Setup(ProfileAudiencePolicy.OwnerLibrary);

        Assert.Equal(LinearUrl, await UrlFor(MapWorld.Human));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_gets_no_link_for_a_record_whose_only_audience_is_a_human_extra()
    {
        await Setup(ProfileAudiencePolicy.OwnerLibrary);

        Assert.Null(await UrlFor(Session()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_gets_the_link_of_a_record_with_its_own_team_s_audience_through_the_same_rig()
    {
        await Setup("linear:ENG");

        Assert.Equal(LinearUrl, await UrlFor(Session()));
    }
}
