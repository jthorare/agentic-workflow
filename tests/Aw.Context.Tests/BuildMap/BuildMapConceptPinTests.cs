using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

public sealed class BuildMapConceptPinTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_concept_pinned_on_the_repo_is_in_the_built_draft_as_an_override_with_its_id_and_name()
    {
        var pin = new ConceptPin(ConceptId.From("scheduling"), "Self scheduling", "People pick their shifts.", ["feature:nothing-matches"], []);
        var repos = new[] { new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { Concepts = [pin] } };

        _ = await _world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, repos), MapWorld.None);

        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var concept = Assert.Single(draft.Graph.Nodes, n => n.Key.Value == "concept:scheduling");
        Assert.Equal("Self scheduling", concept.Label);
        using var attributes = JsonDocument.Parse(concept.Attributes!.Value.GetRawText());
        Assert.Equal("override", attributes.RootElement.GetProperty("source").GetString());
    }
}
