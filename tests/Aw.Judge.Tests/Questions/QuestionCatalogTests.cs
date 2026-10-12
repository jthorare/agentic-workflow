using Aw.Judge.Core.Evaluation;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class QuestionCatalogTests
{
    [Fact]
    public void The_catalog_holds_the_eight_questions()
    {
        Assert.Equal(
            ["ask-check", "brief-scope", "resolution-check", "rule-check", "turn-progress", "ui-element-repair", "visual-critique", "wake-gate"],
            QuestionCatalog.Names.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Each_name_asks_its_own_question(string name)
    {
        var log = new InMemoryDecisionLog();
        var evaluator = new QuestionEvaluator(log, new FakeInputRedactor(), new SequentialIdGenerator(), new FakeTimeProvider(KnownInstants.Epoch), QuestionRun.Json);

        var outcome = await evaluator.Ask(name, "{}", new EvaluationContext { Providers = [] }, CancellationToken.None);

        Assert.Equal(new EscalatedOutcome("invalid-input"), outcome);
        Assert.Equal(name, Assert.Single(log.Decisions).Question);
    }

    public static TheoryData<string> Names() => [.. QuestionCatalog.Names];
}
