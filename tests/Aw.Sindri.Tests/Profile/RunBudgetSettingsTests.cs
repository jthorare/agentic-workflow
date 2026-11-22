using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'workflow.budget.tokens' (#3805): the token budget of a run whose spec sets none; a value below 1 is refused naming the key.</summary>
[Lane(TestLane.Unit)]
public sealed class RunBudgetSettingsTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    private static SindriProfile With(long? tokens) => _valid with { Workflow = new WorkflowSettings { Budget = new RunBudgetSettings(tokens) } };

    [Theory]
    [InlineData(1L)]
    [InlineData(200000L)]
    public void A_budget_above_zero_is_accepted(long tokens) => Assert.True(ProfileRules.Check(With(tokens)).IsSuccess);

    [Fact]
    public void A_budget_block_with_no_tokens_is_accepted() => Assert.True(ProfileRules.Check(With(tokens: null)).IsSuccess);

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void A_budget_below_one_is_refused_naming_the_key(long tokens) =>
        Assert.Contains("workflow.budget.tokens", ProfileRules.Check(With(tokens)).Error.Message, StringComparison.Ordinal);

    [Fact]
    public void The_diff_names_the_key_when_it_changes()
    {
        var changes = ProfileDiff.Between(_valid, With(500));

        Assert.Contains(changes, static c => c.Contains("workflow.budget.tokens", StringComparison.Ordinal));
    }
}
