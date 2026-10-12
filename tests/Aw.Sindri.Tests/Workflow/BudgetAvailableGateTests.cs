using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>'gate.budget.available' (spec §12): real and pure; its refusal parks with 'budget' (§5.5).</summary>
public sealed class BudgetAvailableGateTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Budget_gate_passes_while_tokens_are_left()
    {
        var result = await Evaluate(tokensLeft: 1);

        Assert.Equal(GateVerdict.Pass, result.Verdict);
        Assert.Null(result.Score);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Budget_gate_passes_when_no_budget_applies()
    {
        var result = await Evaluate(tokensLeft: null);

        Assert.Equal(GateVerdict.Pass, result.Verdict);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(-1200)]
    public async Task Budget_gate_refuses_when_the_budget_is_spent(int tokensLeft)
    {
        var result = await Evaluate(tokensLeft);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Equal(["the item's token budget is spent; grant tokens with 'sindri workflow answer <instance> --grant-tokens <n>' at a terminal"], result.Reasons);
    }

    private static async Task<GateResult> Evaluate(int? tokensLeft) =>
        await new BudgetAvailableGate().Evaluate(
            new GateContext(BudgetAvailableGate.Descriptor.Id, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Threshold: null)
            {
                TokensLeft = tokensLeft,
            },
            TestContext.Current.CancellationToken);
}
