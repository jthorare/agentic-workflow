using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Concepts;

/// <summary>
/// Issue 3676: the concepts and the glossary of a map version are built once and stored, so a read answers from the stored rows instead of loading
/// and walking the whole map (23.8 s and 19.6 s on a first read of this repo's map). Counted, not timed: a map load is one snapshot read of the store.
/// </summary>
[Trait("Lane", "unit")]
public sealed class ConceptAnswerReadTests : IDisposable
{
    private static readonly ContextScope App = MapWorld.HumanVia(ContextClient.Http);

    private readonly MapWorld _world = new();
    private int _loads;

    public ConceptAnswerReadTests() => _world.Store.AfterSnapshot = _ => _loads++;

    public void Dispose() => _world.Dispose();

    private static ConceptsQuery FirstPage => new(MapRead.Current, new ConceptPage());

    // Written straight to the store, so no publish step ran: the first read is the one that builds the answer.
    private async Task Saved(ConceptGraph graph) =>
        _ = await _world.Store.SaveCurrent(new MapImport(ContextRepoName.From(FixtureMap.Repo), "abc1234", graph.Build(), []), DateTimeOffset.UnixEpoch, MapWorld.None);

    [Fact]
    public async Task Two_reads_of_the_concepts_load_the_map_once()
    {
        await Saved(ConceptGraph.Product());

        var first = await _world.Engine.Concepts(FirstPage, App, MapWorld.None);
        var second = await _world.Engine.Concepts(FirstPage, App, MapWorld.None);

        Assert.True(first.IsSuccess, first.IsError ? first.Error.Message : null);
        Assert.Equal(["concept:billing", "concept:scheduling"], second.Value.Concepts.Select(c => c.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(1, _loads);
    }

    [Fact]
    public async Task Two_reads_of_the_glossary_load_the_map_once()
    {
        await Saved(new ConceptGraph().ConceptWithTerms("concept:billing", "Billing", "invoice", "refund"));

        var first = await _world.Engine.ConceptGlossary(new ConceptGlossaryQuery(MapRead.Current), App, MapWorld.None);
        var second = await _world.Engine.ConceptGlossary(new ConceptGlossaryQuery(MapRead.Current), App, MapWorld.None);

        Assert.True(first.IsSuccess, first.IsError ? first.Error.Message : null);
        Assert.Equal(["invoice", "refund"], second.Value.Terms.Select(t => t.Term), StringComparer.Ordinal);
        Assert.Equal(1, _loads);
    }
}
