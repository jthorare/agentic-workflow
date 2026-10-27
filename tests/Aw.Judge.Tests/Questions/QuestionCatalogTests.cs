using Aw.Judge.Core.Evaluation;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class QuestionCatalogTests
{
    [Fact]
    public void The_catalog_holds_the_ten_questions()
    {
        Assert.Equal(
            ["ask-check", "brief-scope", "choose", "model-pick", "resolution-check", "rule-check", "turn-progress", "ui-element-repair", "visual-critique", "wake-gate"],
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

        var row = Assert.Single(log.Decisions);
        Assert.Equal(name, row.Question);
        if (string.Equals(name, "choose", StringComparison.Ordinal) || string.Equals(name, "model-pick", StringComparison.Ordinal))
        {
            // why: choose and model-pick alone report the decision id, and it is the recorded row's
            Assert.Equal(new EscalatedOutcome("invalid-input") { Id = row.Id }, outcome);
        }
        else
        {
            Assert.Equal(new EscalatedOutcome("invalid-input"), outcome);
        }
    }

    public static TheoryData<string> Names() => [.. QuestionCatalog.Names];
}
