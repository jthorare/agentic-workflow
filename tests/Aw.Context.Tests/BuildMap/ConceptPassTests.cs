using System.Globalization;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Context.Features.BuildMap;
using Aw.Context.Testing.Glossary;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Concepts;
using Aw.Kernel;

namespace Aw.Context.Tests.BuildMap;

[Trait("Lane", "unit")]
public sealed class ConceptPassTests
{
    private static readonly ContextRepo[] _repos = [new(ContextRepoName.From("demo"), "/work/demo")];
    private readonly FakeConceptHintStore _hints = new();
    private readonly FakeGraphStore _store = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Three areas of three features and one loose feature, each feature implemented by one file.
    private static GraphSnapshot Product()
    {
        var graph = new ConceptGraph();
        foreach (var area in new[] { "sched", "billing", "tabs" })
        {
            _ = graph.Node($"feature:{area}", area).Node($"feature:{area}.one", area + ".one").Node($"feature:{area}.two", area + ".two")
                .Edge($"feature:{area}.one", EdgeType.PartOf, $"feature:{area}").Edge($"feature:{area}.two", EdgeType.PartOf, $"feature:{area}");
        }

        _ = graph.Node("feature:lone", "lone");
        foreach (var feature in new[] { "sched", "billing", "tabs", "tabs.one", "lone" })
        {
            _ = graph.Node($"code:src/{feature}.cs").Edge($"code:src/{feature}.cs", EdgeType.Implements, $"feature:{feature}");
        }

        return graph.Build();
    }

    private static FakeSymbolVectors Vectors() => new FakeSymbolVectors()
        .With("src/billing.cs", "InvoiceTotal", 1f, 0f, 0f)
        .With("src/sched.cs", "ShiftSwap", 0f, 1f, 0f)
        .With("src/tabs.cs", "TabStrip", 0f, 0f, 1f)
        .With("src/tabs.one.cs", "tabStripOrder", 0f, 0f, 1f)
        .With("src/lone.cs", "TabDrag", 0f, 0f, 1f);

    private ValueTask<ConceptOutcome> Run(ISymbolVectors vectors, IConceptNamer? namer = null, GraphSnapshot? graph = null) =>
        new ConceptPass(namer ?? new DisabledConceptNamer(), vectors, _hints, _store).Run(new ContextSettings(), graph ?? Product(), [], _repos, Ct);

    private static List<string> Terms(ConceptOutcome outcome, string concept)
    {
        var node = Assert.Single(outcome.Graph.Nodes, n => n.Key.Value == concept);
        using var document = JsonDocument.Parse(node.Attributes!.Value.GetRawText());
        return document.RootElement.TryGetProperty("terms", out var terms) ? [.. terms.EnumerateArray().Select(t => t.GetString()!)] : [];
    }

    private static bool Belongs(ConceptOutcome outcome, string feature, string concept) =>
        outcome.Graph.Edges.Any(e => e.Type == EdgeType.BelongsTo && e.From.Value == feature && e.To.Value == concept);

    [Fact]
    public async Task The_vectors_of_a_features_files_become_the_terms_of_its_concept_and_group_a_loose_feature()
    {
        var outcome = await Run(Vectors());

        Assert.Contains("strip", Terms(outcome, "concept:tabs"), StringComparer.Ordinal);
        Assert.True(Belongs(outcome, "feature:lone", "concept:tabs"));
        Assert.Empty(outcome.Notes);
    }

    [Fact]
    public async Task A_failed_read_builds_the_concepts_without_a_glossary_and_leaves_a_note()
    {
        var down = Vectors();
        down.Failure = ContextErrors.SourceUnavailable.With("the code index has no embeddings");

        var outcome = await Run(down);

        Assert.Equal(["glossary"], outcome.Notes, StringComparer.Ordinal);
        Assert.Empty(Terms(outcome, "concept:tabs"));
        Assert.True(Belongs(outcome, "feature:lone", "concept:billing"));
    }

    [Fact]
    public async Task A_host_with_no_vectors_builds_without_a_glossary_and_says_nothing()
    {
        var outcome = await Run(new DisabledSymbolVectors());

        Assert.Empty(outcome.Notes);
        Assert.Empty(Terms(outcome, "concept:tabs"));
        Assert.True(Belongs(outcome, "feature:lone", "concept:billing"));
    }

    [Fact]
    public async Task A_map_with_no_code_asks_for_no_vectors()
    {
        var vectors = Vectors();
        var features = new ConceptGraph().Node("feature:a", "a").Node("feature:b", "b").Build();

        _ = await Run(vectors, graph: features);

        Assert.Empty(vectors.Asked);
    }

    [Fact]
    public async Task Paths_are_asked_in_reads_of_at_most_two_thousand()
    {
        var vectors = new FakeSymbolVectors();
        var graph = new ConceptGraph().Node("feature:big", "big");
        foreach (var i in Enumerable.Range(0, 2001))
        {
            var file = string.Create(CultureInfo.InvariantCulture, $"code:src/f{i}.cs");
            _ = graph.Node(file).Edge(file, EdgeType.Implements, "feature:big");
        }

        _ = await Run(vectors, graph: graph.Build());

        Assert.Equal([2000, 1], vectors.Asked);
    }

    [Fact]
    public async Task The_namer_is_given_each_groups_terms()
    {
        var namer = new CapturingNamer();

        _ = await Run(Vectors(), namer);

        var tabs = Assert.Single(namer.Groups, g => g.Seed == "feature:tabs");
        Assert.Contains("strip", tabs.Terms, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_namer_that_cannot_answer_leaves_the_concepts_note_beside_the_glossary()
    {
        var outcome = await Run(Vectors(), new CapturingNamer { Failure = ContextErrors.SourceUnavailable.With("no model") });

        Assert.Equal(["concepts"], outcome.Notes, StringComparer.Ordinal);
        Assert.Contains("strip", Terms(outcome, "concept:tabs"), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_last_scans_hints_shape_the_concepts()
    {
        var draft = await _store.SaveDraft(new MapImport(ContextRepoName.From("demo"), "abc123", Product(), []), DateTimeOffset.UnixEpoch, Ct);
        _ = await _hints.Save(draft.Value.Number, [new ConceptHint("Tab work", ["feature:tabs", "feature:sched.one", "feature:billing.one"], [], 0.9)], DateTimeOffset.UnixEpoch, Ct);

        var outcome = await Run(new DisabledSymbolVectors());

        Assert.True(Belongs(outcome, "feature:sched.one", "concept:tabs"));
        Assert.True(Belongs(outcome, "feature:billing.one", "concept:tabs"));
    }

    [Fact]
    public async Task A_host_whose_hint_store_cannot_answer_builds_without_hints_and_without_a_note()
    {
        var outcome = await new ConceptPass(new DisabledConceptNamer(), new DisabledSymbolVectors(), new NoHints(), _store).Run(new ContextSettings(), Product(), [], _repos, Ct);

        Assert.Empty(outcome.Notes);
        Assert.True(Belongs(outcome, "feature:sched.one", "concept:sched"));
    }

    private sealed class NoHints : IConceptHintStore
    {
        public ValueTask<Result<Unit>> Save(int version, IReadOnlyList<ConceptHint> hints, DateTimeOffset scanned, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<Unit>>(ContextErrors.StoreUnavailable.With("no store"));

        public ValueTask<Result<IReadOnlyList<ConceptHint>>> Read(int version, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<IReadOnlyList<ConceptHint>>>(ContextErrors.StoreUnavailable.With("no store"));
    }

    private sealed class CapturingNamer : IConceptNamer
    {
        public IReadOnlyList<ConceptGroup> Groups { get; private set; } = [];

        public Error? Failure { get; init; }

        public ValueTask<Result<ConceptNames>> Name(IReadOnlyList<ConceptGroup> groups, IReadOnlyList<ScrubPattern> extraPatterns, int maxTokens, CancellationToken cancellationToken)
        {
            Groups = groups;
            return ValueTask.FromResult<Result<ConceptNames>>(Failure is { } failure ? failure : ConceptNames.None);
        }
    }
}
