using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Features.BuildMap;
using Aw.Context.Infrastructure.Labeling;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Concepts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.BuildMap;

[Trait("Lane", "unit")]
public sealed class ConceptNamerNoteTests
{
    private static readonly ContextRepo[] _repos = [new(ContextRepoName.From("demo"), "/work/demo")];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Two areas of two features each, so the deriver has groups for the namer to name.
    private static GraphSnapshot Product()
    {
        var graph = new ConceptGraph();
        foreach (var area in new[] { "sched", "billing" })
        {
            _ = graph.Node($"feature:{area}", area).Node($"feature:{area}.one", area + ".one").Node($"feature:{area}.two", area + ".two")
                .Edge($"feature:{area}.one", EdgeType.PartOf, $"feature:{area}").Edge($"feature:{area}.two", EdgeType.PartOf, $"feature:{area}");
            _ = graph.Node($"code:src/{area}.cs").Edge($"code:src/{area}.cs", EdgeType.Implements, $"feature:{area}");
        }

        return graph.Build();
    }

    private static ValueTask<ConceptOutcome> Run(FakeModelPort model, int maxTokens) =>
        new ConceptPass(
            new ModelConceptNamer(model, new RedactionScrubberFactory(new FakeTimeProvider())),
            new DisabledSymbolVectors(),
            new FakeConceptHintStore(),
            new FakeGraphStore()).Run(
            new ContextSettings { Labeling = new LabelingSettings { Enabled = true, MaxTokens = maxTokens } }, Product(), [], _repos, Ct);

    [Fact]
    public async Task A_namer_over_the_token_cap_leaves_a_note_that_names_the_cap()
    {
        var model = new FakeModelPort();

        var outcome = await Run(model, 10);

        var note = Assert.Single(outcome.Notes);
        Assert.StartsWith("concepts: ", note, StringComparison.Ordinal);
        Assert.Contains(ContextErrors.LabelingUnavailable.Code, note, StringComparison.Ordinal);
        Assert.Contains("cap", note, StringComparison.Ordinal);
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task A_failed_model_call_leaves_a_note_with_the_models_error_code()
    {
        var model = new FakeModelPort { Fail = _ => ContextErrors.SourceUnavailable.With("no model") };

        var outcome = await Run(model, 400_000);

        var note = Assert.Single(outcome.Notes);
        Assert.StartsWith("concepts: ", note, StringComparison.Ordinal);
        Assert.Contains(ContextErrors.SourceUnavailable.Code, note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_namer_that_succeeds_leaves_no_note()
    {
        var model = new FakeModelPort { Answer = """[{"id":"g1","name":"Self scheduling","summary":"People pick shifts."}]""" };

        var outcome = await Run(model, 400_000);

        Assert.Empty(outcome.Notes);
        Assert.NotEmpty(model.Requests);
    }
}
