using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.WholeMap;
using Aw.Context.Infrastructure.ConceptAnswers;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Kernel.Json;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Concepts;

/// <summary>Issue 3676: an answer read back from concept_answer writes the same JSON as the answer built from the map, so the routes' bytes don't change.</summary>
[Trait("Lane", "integration")]
public sealed class ConceptAnswerRoundTripTests : IDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(ConceptAnswerJsonContext.Default);

    private readonly TempHome _home = new();
    private readonly ContextDatabase _database;
    private readonly SqliteGraphStore _graphs;
    private readonly SqliteConceptAnswerStore _answers;

    public ConceptAnswerRoundTripTests()
    {
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        _database = new ContextDatabase(settings, new FakeTimeProvider());
        _graphs = new SqliteGraphStore(_database, settings);
        _answers = new SqliteConceptAnswerStore(_database, new FakeTimeProvider());
    }

    public void Dispose()
    {
        _database.Dispose();
        _home.Dispose();
    }

    private static PinSet Pins()
    {
        var billing = ConceptPin.Of(ConceptId.From("billing"), "Billing", "Charges and refunds.", features: null, paths: ["services/billing/**"]).WithAliases(["payments", "invoice"]);
        var repo = new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { Concepts = [billing] };
        return PinSet.Of([repo], [repo], ConceptGraph.Product().Build());
    }

    private async Task<ConceptAnswerBasis> Basis()
    {
        _ = await _graphs.SaveCurrent(new MapImport(ContextRepoName.From(FixtureMap.Repo), "abc1234", ConceptGraph.Product().Build(), []), DateTimeOffset.UnixEpoch, MapWorld.None);
        var basis = await _answers.Basis(MapRead.Current, MapWorld.None);
        Assert.NotNull(basis.Value);
        return basis.Value;
    }

    [Fact]
    public async Task A_kept_concepts_answer_reads_back_as_the_same_json()
    {
        var graph = ConceptGraph.Product().Build();
        var view = ConceptBuilder.Build(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, new ConceptPage(Limit: 1)), graph, ConceptGraph.Text, Pins()).Value;
        var built = new KeptConcepts(view, ConceptBuilder.CodePathsOf(view.Concepts.Select(c => c.Id), graph));

        _ = await _answers.Keep("k", await Basis(), built, MapWorld.None);
        var read = await _answers.Concepts("k", MapWorld.None);

        Assert.NotNull(read.Value);
        Assert.NotNull(view.Next);
        Assert.NotEmpty(built.Paths);
        Assert.Equal(AwJson.Serialize(view, _json), AwJson.Serialize(read.Value.View, _json));
        Assert.Equal(built.Paths, read.Value.Paths);
    }

    // Review of PR 3881: every built answer has no 'code' and a last page has no 'next', and both are left out of the JSON; the read must still answer, not miss on every read.
    [Fact]
    public async Task A_kept_last_page_with_no_code_and_no_next_reads_back()
    {
        var graph = ConceptGraph.Product().Build();
        var view = ConceptBuilder.Build(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, new ConceptPage()), graph, ConceptGraph.Text).Value;
        var built = new KeptConcepts(view, ConceptBuilder.CodePathsOf(view.Concepts.Select(c => c.Id), graph));

        _ = await _answers.Keep("last", await Basis(), built, MapWorld.None);
        var read = await _answers.Concepts("last", MapWorld.None);

        Assert.Null(view.Code);
        Assert.Null(view.Next);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        Assert.NotNull(read.Value);
        Assert.Equal(AwJson.Serialize(view, _json), AwJson.Serialize(read.Value.View, _json));
    }

    // #3696: the repo picker's list is part of the kept answer; a body kept before it (no 'repos') still reads back, as the last-page test above shows.
    [Fact]
    public async Task A_kept_answer_reads_back_its_repos()
    {
        var graph = ConceptGraph.Product().Build();
        var view = ConceptBuilder.Build(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, new ConceptPage()), graph, ConceptGraph.Text).Value
            with { Repos = [new WholeMapRepo("aw-split-panes", 4), new WholeMapRepo("other", 1)] };
        var built = new KeptConcepts(view, ConceptBuilder.CodePathsOf(view.Concepts.Select(c => c.Id), graph));

        _ = await _answers.Keep("repos", await Basis(), built, MapWorld.None);
        var read = await _answers.Concepts("repos", MapWorld.None);

        Assert.NotNull(read.Value);
        Assert.Equal(view.Repos, read.Value.View.Repos);
        Assert.Equal(AwJson.Serialize(view, _json), AwJson.Serialize(read.Value.View, _json));
    }

    [Fact]
    public async Task A_kept_glossary_answer_reads_back_as_the_same_json()
    {
        var graph = new ConceptGraph().ConceptWithTerms("concept:billing", "Billing", "invoice", "refund").ConceptWithTerms("concept:auth", "Auth", "login").Build();
        var built = ConceptGlossaryBuilder.Build(ConceptGraph.Basis, graph, ConceptGraph.Text, Pins());

        _ = await _answers.Keep("g", await Basis(), built, MapWorld.None);
        var read = await _answers.Glossary("g", MapWorld.None);

        Assert.NotNull(read.Value);
        Assert.Equal(AwJson.Serialize(built, _json), AwJson.Serialize(read.Value, _json));
    }
}
