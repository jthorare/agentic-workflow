using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

/// <summary>Reads of a retained map version (scoping spec C10): the stored graph, but today's deny globs, overrides and session scope on it.</summary>
public sealed class AsOfReadTests : IDisposable
{
    private const string Denied = "src/layout/SplitPane.ts";
    private readonly MapWorld _world = new();
    private readonly MapReader _reader;

    public AsOfReadTests()
    {
        _reader = new MapReader(_world.Store, _world.Intents);
    }

    public void Dispose() => _world.Dispose();

    private static MapRead At(string text) => MapRead.Of(draft: false, text).Value;

    private static ContextScope Denying(params string[] globs) =>
        new(Settings: null, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, .. globs] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    private async Task TwoVersions()
    {
        _ = await _world.ImportFixture();   // v1
        _ = await _world.ImportYaml(MapWorld.Yaml("schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:", "  - {key: \"feature:only-in-two\", label: \"Two\"}"));   // v2
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_retained_version_reads_with_its_own_graph_and_label()
    {
        await TwoVersions();

        var old = await _reader.ReadViewAt(MapWorld.Human, At("v1"), MapWorld.None);
        var current = await _reader.ReadViewAt(MapWorld.Human, MapRead.Current, MapWorld.None);

        Assert.Equal((1, "v1"), (old.Value.Snapshot.Version.Number, old.Value.Snapshot.Tag));
        Assert.Equal(2, current.Value.Snapshot.Version.Number);
        Assert.Contains(old.Value.Graph.Nodes, n => string.Equals(n.Key.Value, "feature:layout.split-pane", StringComparison.Ordinal));
        Assert.DoesNotContain(old.Value.Graph.Nodes, n => string.Equals(n.Key.Value, "feature:only-in-two", StringComparison.Ordinal));
        Assert.Contains(current.Value.Graph.Nodes, n => string.Equals(n.Key.Value, "feature:only-in-two", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_version_never_written_is_120()
    {
        await TwoVersions();

        var result = await _reader.ReadViewAt(MapWorld.Human, At("v99"), MapWorld.None);

        Assert.Equal(ContextErrors.MapVersionNotRetained.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_version_the_purge_removed_is_120_naming_the_current_version_and_the_retained_range()
    {
        for (var i = 0; i < MapRetention.RetainedVersions + 3; i++)
        {
            _ = await _world.ImportFixture();
        }

        _world.Clock.Advance(TimeSpan.FromDays(91));
        _ = await _world.Map.PurgeExpired(MapWorld.None);

        var purged = await _reader.ReadViewAt(MapWorld.Human, At("v1"), MapWorld.None);
        var kept = await _reader.ReadViewAt(MapWorld.Human, At("v3"), MapWorld.None);

        Assert.Equal(ContextErrors.MapVersionNotRetained.Code, purged.Error.Code);
        Assert.Contains("v" + (MapRetention.RetainedVersions + 3).ToString(System.Globalization.CultureInfo.InvariantCulture), purged.Error.Message, StringComparison.Ordinal);
        Assert.Contains("v3", purged.Error.Message, StringComparison.Ordinal);
        Assert.True(kept.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task As_of_read_applies_todays_extra_scrub_pattern()
    {
        const string Distinct = "ACME-123456";
        const string record = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02";
        const string recordId = "linear:issue/0e5a1b00-0000-4000-8000-000000000a01";
        var decision = DecisionRow.KeyFor(RecordKey.From(recordId), "keep the pane in view").Value;
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:in\", label: \"In scope\"}",
            "  - {key: \"code:src/in.ts\", label: \"in.ts\", provenance: deterministic}",
            $"  - {{key: \"{record}\", label: \"Title {Distinct}\"}}",
            $"  - {{key: \"record:{recordId}\", label: \"Quoted record\"}}",
            "edges:",
            "  - {from: \"code:src/in.ts\", to: \"feature:in\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/in.ts\"}]}",
            $"  - {{from: \"{record}\", to: \"feature:in\", type: mentions}}",
            $"  - {{from: \"{decision}\", to: \"feature:in\", type: decides}}",
            "decisions:",
            $"  - {{record: \"{recordId}\", quote: \"keep the pane in view\", wording: \"Keep it {Distinct}.\", at: 2026-07-15T10:00:00Z}}"));   // v1
        _ = await _world.ImportYaml(MapWorld.Yaml("schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:", "  - {key: \"feature:only-in-two\", label: \"Two\"}"));   // v2
        using var section = JsonDocument.Parse("""{"sessions":{"enabled":true},"scrub":{"extraPatterns":[{"id":"acme-id","pattern":"ACME-[0-9]{6}"}]}}""");
        var session = new ContextScope(section.RootElement.Clone(), MapWorld.Repos, new ContextCaller(ContextCallerKind.Session, ContextRepoName.From(FixtureMap.Repo), ContextClient.Http));

        var one = await _world.Engine.Lenses("feature:in", At("v1"), depth: null, session, MapWorld.None);
        var set = await _world.Engine.LensesOf(["feature:in", record], At("v1"), depth: null, session, MapWorld.None);
        var query = await _world.Engine.Query(new ContextQuery(Text: null, Node: record) { MapVersion = "v1" }, session, MapWorld.None);

        var all = new[] { JsonSerializer.Serialize(one.Value), JsonSerializer.Serialize(set.Value), JsonSerializer.Serialize(query.Value) };
        Assert.All(all, json => Assert.DoesNotContain(Distinct, json, StringComparison.Ordinal));
        Assert.All(all, json => Assert.Contains("[REDACTED:acme-id]", json, StringComparison.Ordinal));
        Assert.NotEmpty(set.Value.Bands.Intent.Records);
        Assert.All(set.Value.Bands.Intent.Records, r => Assert.StartsWith("<untrusted source=\"map\">", r.Label, StringComparison.Ordinal));
        var record1 = Assert.Single(one.Value.Bands.Intent.Records);
        Assert.Equal((NodeKind.Record, Trust.Untrusted), (record1.Kind, record1.Trust));
        Assert.StartsWith("<untrusted source=\"map\">", record1.Label, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_single_retained_version_is_named_once_not_as_a_range()
    {
        await TwoVersions();

        var result = await _reader.ReadViewAt(MapWorld.Human, At("v99"), MapWorld.None);

        Assert.Contains("retained: v1)", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("v1..v1", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_commit_part_that_differs_is_120_and_the_label_an_answer_printed_reads_back()
    {
        var store = _world.Store;
        _ = await store.SaveCurrent(SampleMap.Build() with { Commit = "a41b07d9c0ffee" }, _world.Clock.GetUtcNow(), MapWorld.None);

        var same = await _reader.ReadViewAt(MapWorld.Human, At("v1@a41b07d"), MapWorld.None);
        var other = await _reader.ReadViewAt(MapWorld.Human, At("v1@b41b07d"), MapWorld.None);

        Assert.True(same.IsSuccess);
        Assert.Equal(ContextErrors.MapVersionNotRetained.Code, other.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_is_not_held_by_number()
    {
        _ = await _world.ImportFixture();
        var draft = (await _world.Store.SaveDraft(SampleMap.Build(), _world.Clock.GetUtcNow(), MapWorld.None)).Value;

        var result = await _reader.ReadViewAt(MapWorld.Human, At(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"v{draft.Number}")), MapWorld.None);

        Assert.Equal(ContextErrors.MapVersionNotRetained.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task As_of_read_applies_todays_deny_globs()
    {
        _ = await _world.ImportFixture();
        _ = await _world.ImportFixture();

        var before = await _reader.ReadViewAt(MapWorld.Human, At("v1"), MapWorld.None);
        var after = await _reader.ReadViewAt(Denying(Denied), At("v1"), MapWorld.None);

        Assert.Contains(before.Value.Graph.Nodes, n => string.Equals(n.Key.Value, $"code:{Denied}", StringComparison.Ordinal));
        Assert.DoesNotContain(after.Value.Graph.Nodes, n => string.Equals(n.Key.Value, $"code:{Denied}", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_as_of_read_is_scoped_on_that_versions_graph()
    {
        await TwoVersions();

        var own = await _reader.ReadViewAt(MapWorld.Session(FixtureMap.Repo), At("v1"), MapWorld.None);
        var elsewhere = await _reader.ReadViewAt(MapWorld.Session("other"), At("v1"), MapWorld.None);
        var off = await _reader.ReadViewAt(MapWorld.Session(FixtureMap.Repo, enabled: false), At("v1"), MapWorld.None);

        Assert.True(own.IsSuccess);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, elsewhere.Error.Code);
        Assert.Equal(ContextErrors.SessionsDisabled.Code, off.Error.Code);
    }
}
