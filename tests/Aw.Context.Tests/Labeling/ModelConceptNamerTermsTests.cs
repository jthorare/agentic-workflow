using Aw.Context.Core.Concepts;
using Aw.Context.Infrastructure.Labeling;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Testing.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Labeling;

[Trait("Lane", "unit")]
public sealed class ModelConceptNamerTermsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ModelConceptNamer Namer(FakeModelPort model) => new(model, new RedactionScrubberFactory(new FakeTimeProvider()));

    [Fact]
    public async Task A_group_with_terms_tells_the_model_the_words_its_code_uses()
    {
        var model = new FakeModelPort { Answer = "[]" };
        var group = new ConceptGroup("feature:tabs", "Tabs", ["Tab strip"], []) { Terms = ["strip", "drag"] };

        _ = await Namer(model).Name([group], [], 10_000, Ct);

        var prompt = Assert.Single(model.Requests).Prompt;
        Assert.Contains("features: Tab strip | terms: strip; drag", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_group_without_terms_asks_exactly_what_it_asked_before()
    {
        var model = new FakeModelPort { Answer = "[]" };

        _ = await Namer(model).Name([new ConceptGroup("feature:tabs", "Tabs", ["Tab strip"], ["Tab spec"])], [], 10_000, Ct);

        var prompt = Assert.Single(model.Requests).Prompt;
        Assert.Contains("g1: features: Tab strip | records: Tab spec", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("terms", prompt, StringComparison.Ordinal);
    }
}
