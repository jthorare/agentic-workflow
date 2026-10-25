using Aw.Context.Core.Concepts;
using Aw.Context.Core.Models;
using Aw.Context.Infrastructure.Labeling;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Testing.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Labeling;

[Trait("Lane", "unit")]
public sealed class ModelConceptNamerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ModelConceptNamer Namer(IModelPort model) => new(model, new RedactionScrubberFactory(new FakeTimeProvider()));

    private static ConceptGroup Group(string seed, string[] features, params string[] records) => new(seed, seed, features, records);

    [Fact]
    public async Task The_prompt_carries_titles_and_never_paths_and_the_answer_names_the_seed()
    {
        var model = new FakeModelPort { Answer = """[{"id":"g1","name":" Self  scheduling ","summary":"People pick shifts."},{"id":"zz","name":"Unknown"}]""" };

        var names = await Namer(model).Name([Group("feature:sched", ["Shift swaps", "Open shifts"], "Swap requests")], [], 10_000, Ct);

        var request = Assert.Single(model.Requests);
        Assert.Equal(ModelPurpose.Concepts, request.Purpose);
        Assert.Contains("Shift swaps; Open shifts", request.Prompt, StringComparison.Ordinal);
        Assert.Contains("Swap requests", request.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("feature:sched", request.Prompt, StringComparison.Ordinal);
        var name = Assert.Single(names.Value.BySeed);
        Assert.Equal(("feature:sched", "People pick shifts."), (name.Key, name.Value.Summary));
    }

    [Fact]
    public async Task A_model_that_cannot_answer_or_a_cap_that_is_too_small_names_nothing_without_failing()
    {
        var down = new FakeModelPort { Fail = _ => Aw.Context.Core.ContextErrors.SourceUnavailable.With("no model") };
        var quiet = new FakeModelPort();

        var failed = await Namer(down).Name([Group("feature:a", ["A"])], [], 10_000, Ct);
        var capped = await Namer(quiet).Name([Group("feature:a", ["A"])], [], 10, Ct);

        Assert.Empty(failed.Value.BySeed);
        Assert.Empty(capped.Value.BySeed);
        Assert.Empty(quiet.Requests);
    }
}
