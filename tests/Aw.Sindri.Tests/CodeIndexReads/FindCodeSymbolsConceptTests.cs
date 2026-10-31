using Aw.CodeIndex.Core.Facade;
using CodeSymbolKind = Aw.CodeIndex.Core.Model.SymbolKind;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Linking;
using Aw.Context.Testing.Map;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CodeIndexReads;
using Aw.Sindri.Features.ContextCalls;
using CodeRepoName = Aw.CodeIndex.Core.Values.RepoName;
using ContextRepoName = Aw.Context.Core.Engine.ContextRepoName;
using MapRecordKey = Aw.Context.Core.Sources.RecordKey;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>
/// A symbol search inside one concept pin (concept pins spec §3.5): the files the pin holds in the map become the search's Within set, so only in-pin hits come back; the pin lookup's
/// errors (102, 105) pass through; and the notes say which map answered and when the map and the index are at different commits.
/// </summary>
public sealed class FindCodeSymbolsConceptTests
{
    private static readonly string _mapCommit = new('a', 40);
    private readonly SliceHarness _h = new();
    private readonly FakeGraphStore _graphs = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SymbolHit Hit(string repo, string file) =>
        new(SymbolRef.From($"{repo}:{file}#Pay.Refund"), CodeSymbolKind.Method, "Refund", file, 1, 5, "void Refund()", Exported: true);

    // Pin 'billing' holds demo's src/pay.cs and other's lib/pay.cs through home features that belong to it; src/auth.cs is in no pin.
    private static GraphSnapshot Map()
    {
        var concept = NodeKey.From("concept:billing");
        var demoHome = NodeKey.From("feature:demo.src");
        var otherHome = NodeKey.From("feature:other.lib");
        var authHome = NodeKey.From("feature:demo.auth");
        GraphNode[] nodes =
        [
            new(concept, "Billing", Provenance.Deterministic),
            new(demoHome, "src", Provenance.Deterministic),
            new(otherHome, "lib", Provenance.Deterministic),
            new(authHome, "auth", Provenance.Deterministic),
            Code("src/pay.cs", "demo"),
            Code("lib/pay.cs", "other"),
            Code("src/auth.cs", "demo"),
        ];
        GraphEdge[] edges =
        [
            new(demoHome, concept, EdgeType.BelongsTo, Provenance.Deterministic, 1.0),
            new(otherHome, concept, EdgeType.BelongsTo, Provenance.Deterministic, 1.0),
            new(NodeKey.From("code:src/pay.cs"), demoHome, EdgeType.Implements, Provenance.Deterministic, 1.0),
            new(NodeKey.From("code:lib/pay.cs"), otherHome, EdgeType.Implements, Provenance.Deterministic, 1.0),
            new(NodeKey.From("code:src/auth.cs"), authHome, EdgeType.Implements, Provenance.Deterministic, 1.0),
        ];
        return new GraphSnapshot(nodes, edges, [], []);
    }

    private static GraphNode Code(string path, string repo) =>
        new(NodeKey.From("code:" + path), path, Provenance.Deterministic, 1.0, [new EvidenceRef(MapRecordKey.From($"repo:{repo}/{path}"), "L1")]);

    private async Task Approve(bool withPin = true)
    {
        var pin = new ConceptSettings(Id: ConceptPinId.From("billing"), Name: "Billing", Paths: ["src/pay.cs"]);
        var profile = ProfileRules.Check(new SindriProfile(1, "thor",
            Index: withPin ? new IndexSettings(Concepts: [pin]) : null,
            Repos: [new RepoEntry("demo", "/work/demo"), new RepoEntry("other", "/work/other")])).Value;
        _ = await _h.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Ct);
    }

    private async Task Build(string? mapCommit) =>
        _ = await _graphs.SaveCurrent(new MapImport(ContextRepoName.From("demo"), mapCommit, Map(), []), DateTimeOffset.UnixEpoch, Ct);

    private PinnedCodeReader Reader() => new(new ContextScopes(_h.Profiles, _h.Callers), new MapReader(_graphs, new FakeIntentStore()));

    private static FakeCodeIndex Index(string indexCommit) => new FakeCodeIndex()
        .WithIndex(CodeRepoName.From("demo"), CommitSha.From(indexCommit), Hit("demo", "src/pay.cs"), Hit("demo", "src/auth.cs"))
        .WithIndex(CodeRepoName.From("other"), CommitSha.From(indexCommit), Hit("other", "lib/pay.cs"));

    private async Task<Result<FindSymbolsAnswer>> Find(FakeCodeIndex index, string? concept)
    {
        using var caller = _h.Callers.EnterService("test");
        var service = new FindCodeSymbolsService(new CodeIndexScopes(_h.Profiles, _h.Profiles, _h.Callers), index, Reader());
        return await service.Handle(new FindCodeSymbolsRequest("Refund", Concept: concept), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_reader_lists_the_pins_code_files_per_repo()
    {
        await Approve();
        await Build(_mapCommit);
        using var caller = _h.Callers.EnterService("test");

        var pinned = (await Reader().Files("concept:billing", Ct)).Value;

        Assert.Equal(("billing", "v1"), (pinned.Pin, pinned.MapTag));
        Assert.Equal([("demo", "src/pay.cs"), ("other", "lib/pay.cs")], pinned.Files.Select(static f => (f.Repo, f.Path)).Order());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_in_pin_hits_come_back_under_a_concept()
    {
        await Approve();
        await Build(_mapCommit);

        var all = (await Find(Index(_mapCommit), concept: null)).Value;
        var pinned = (await Find(Index(_mapCommit), "billing")).Value;

        Assert.Equal(3, all.Hits.Count);
        Assert.Equal(["lib/pay.cs", "src/pay.cs"], pinned.Hits.Select(static h => h.File).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains("scoped to concept billing (map v1)", pinned.Notes, StringComparer.Ordinal);
        Assert.Empty(all.Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pin_with_no_matching_symbol_says_none_in_words()
    {
        await Approve();
        await Build(_mapCommit);
        using var caller = _h.Callers.EnterService("test");
        var service = new FindCodeSymbolsService(new CodeIndexScopes(_h.Profiles, _h.Profiles, _h.Callers), Index(_mapCommit), Reader());

        var found = (await service.Handle(new FindCodeSymbolsRequest("Nothing", Concept: "billing"), Ct)).Value;

        Assert.Empty(found.Hits);
        Assert.Contains("concept billing: no symbols named 'Nothing' in it", found.Notes, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_pin_is_102_and_no_map_is_105()
    {
        await Approve();
        await Build(_mapCommit);
        var unknown = await Find(Index(_mapCommit), "nope");

        var empty = new FindCodeSymbolsConceptTests();
        await empty.Approve();
        var noMap = await empty.Find(Index(_mapCommit), "billing");

        Assert.Equal("SND-CONTEXT-102", unknown.Error.Code);
        Assert.Equal("SND-CONTEXT-105", noMap.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task The_different_commits_note_shows_only_when_the_index_and_the_map_differ(bool same, bool noted)
    {
        await Approve();
        await Build(_mapCommit);

        var found = (await Find(Index(same ? _mapCommit : new string('b', 40)), "billing")).Value;

        Assert.Equal(noted, found.Notes.Any(static n => n.Contains("different commits", StringComparison.Ordinal)));
    }
}
