using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Concepts;

/// <summary>Issue 3676: publish fills the kept concepts and glossary answers, and any input the answer reads that changes gives a fresh answer, never a kept stale one.</summary>
[Trait("Lane", "unit")]
public sealed class ConceptAnswerKeepTests : IDisposable
{
    private static readonly ContextScope App = MapWorld.HumanVia(ContextClient.Http);

    private readonly MapWorld _world = new();
    private int _loads;

    public void Dispose() => _world.Dispose();

    private static ConceptsQuery FirstPage => new(MapRead.Current, new ConceptPage());

    private void CountLoads() => _world.Store.AfterSnapshot = _ => _loads++;

    private async Task<MapVersion> Saved(ConceptGraph graph) =>
        (await _world.Store.SaveCurrent(new MapImport(ContextRepoName.From(FixtureMap.Repo), "abc1234", graph.Build(), []), DateTimeOffset.UnixEpoch, MapWorld.None)).Value;

    private static ContextScope Pinned(string summary)
    {
        var pin = ConceptPin.Of(ConceptId.From("billing"), "Billing", summary, features: null, paths: ["services/billing/**"]).WithAliases(["payments"]);
        return App with { Repos = [MapWorld.Repos[0] with { Concepts = [pin] }, MapWorld.Repos[1]] };
    }

    [Fact]
    public async Task An_import_fills_the_answers_so_the_first_read_loads_no_map()
    {
        _ = await _world.ImportFixture();
        CountLoads();

        var concepts = await _world.Engine.Concepts(FirstPage, App, MapWorld.None);
        var glossary = await _world.Engine.ConceptGlossary(new ConceptGlossaryQuery(MapRead.Current), App, MapWorld.None);

        Assert.True(concepts.IsSuccess, concepts.IsError ? concepts.Error.Message : null);
        Assert.True(glossary.IsSuccess, glossary.IsError ? glossary.Error.Message : null);
        Assert.Equal(0, _loads);
    }

    [Fact]
    public async Task A_promote_fills_the_answers_of_the_new_current_version()
    {
        _ = await _world.Store.SaveDraft(new MapImport(ContextRepoName.From(FixtureMap.Repo), "abc1234", ConceptGraph.Product().Build(), []), DateTimeOffset.UnixEpoch, MapWorld.None);
        var promoted = await _world.Map.Promote(MapWorld.Human, MapWorld.None);
        CountLoads();

        var concepts = await _world.Engine.Concepts(FirstPage, App, MapWorld.None);

        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
        Assert.Equal(MapVersionRef.Tag(promoted.Value.Version.Number), concepts.Value.MapVersion.Split('@')[0]);
        Assert.Equal(2, concepts.Value.Concepts.Count);
        Assert.Equal(0, _loads);
    }

    [Fact]
    public async Task A_pin_edit_in_the_profile_answers_fresh_and_never_the_kept_answer()
    {
        _ = await Saved(ConceptGraph.Product());

        var before = await _world.Engine.Concepts(FirstPage, Pinned("Charges and refunds."), MapWorld.None);
        var after = await _world.Engine.Concepts(FirstPage, Pinned("Everything we bill."), MapWorld.None);
        var glossary = await _world.Engine.ConceptGlossary(new ConceptGlossaryQuery(MapRead.Current), Pinned("Everything we bill."), MapWorld.None);

        Assert.Equal("Charges and refunds.", before.Value.Concepts.Single(c => c.Id.Value == "concept:billing").Summary);
        Assert.Equal("Everything we bill.", after.Value.Concepts.Single(c => c.Id.Value == "concept:billing").Summary);
        Assert.Equal(["payments"], glossary.Value.Concepts.Single(c => c.Id.Value == "concept:billing").OwnerTerms, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_second_version_never_answers_with_the_first_versions_rows()
    {
        var first = await Saved(new ConceptGraph().ConceptWithTerms("concept:billing", "Billing", "invoice"));
        var v1 = await _world.Engine.Concepts(FirstPage, App, MapWorld.None);
        var g1 = await _world.Engine.ConceptGlossary(new ConceptGlossaryQuery(MapRead.Current), App, MapWorld.None);
        var second = await Saved(new ConceptGraph().ConceptWithTerms("concept:auth", "Auth", "login"));

        var v2 = await _world.Engine.Concepts(FirstPage, App, MapWorld.None);
        var g2 = await _world.Engine.ConceptGlossary(new ConceptGlossaryQuery(MapRead.Current), App, MapWorld.None);
        var retained = await _world.Engine.Concepts(new ConceptsQuery(new MapRead(Draft: false, new MapVersionRef(first.Number, Commit: null)), new ConceptPage()), App, MapWorld.None);

        Assert.Equal(["concept:billing"], v1.Value.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
        Assert.Equal(["concept:auth"], v2.Value.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
        Assert.Equal(["invoice"], g1.Value.Terms.Select(t => t.Term), StringComparer.Ordinal);
        Assert.Equal(["login"], g2.Value.Terms.Select(t => t.Term), StringComparer.Ordinal);
        Assert.NotEqual(first.Number, second.Number);
        Assert.Equal(["concept:billing"], retained.Value.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_override_appended_after_a_read_answers_with_the_renamed_concept()
    {
        _ = await Saved(ConceptGraph.Product());
        _ = await _world.Engine.Concepts(FirstPage, App, MapWorld.None);
        _ = await _world.Store.AppendOverrides([new MapOverride(OverrideAction.Rename, NodeKey.From("concept:billing"), "avery", DateTimeOffset.UnixEpoch, "Payments")], MapWorld.None);

        var renamed = await _world.Engine.Concepts(FirstPage, App, MapWorld.None);

        Assert.Equal("Payments", renamed.Value.Concepts.Single(c => c.Id.Value == "concept:billing").Name);
    }

    [Fact]
    public async Task Another_client_or_caller_gets_its_own_answer_and_a_search_is_never_kept()
    {
        _ = await Saved(ConceptGraph.Product());
        CountLoads();

        _ = await _world.Engine.Concepts(FirstPage, App, MapWorld.None);
        _ = await _world.Engine.Concepts(FirstPage, MapWorld.HumanVia(ContextClient.Mcp), MapWorld.None);
        var searched = await _world.Engine.Concepts(FirstPage with { Q = "shift" }, App, MapWorld.None);
        _ = await _world.Engine.Concepts(FirstPage with { Q = "shift" }, App, MapWorld.None);

        Assert.Equal(["concept:scheduling"], searched.Value.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
        Assert.Equal(4, _loads);
    }
}
