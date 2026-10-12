using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// 'gate.budget.available' (spec §12, real and pure): passes while the item has tokens left to spend. Its refusal
/// parks the instance with 'budget' (§5.5), which '--grant-tokens' answers.
/// </summary>
internal sealed class BudgetAvailableGate : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("gate.budget.available"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.None, Parameters: [], Rung.Deterministic, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context.TokensLeft is null or > 0 ? Pass : Spent);
    }

    private static GateResult Pass { get; } = new(GateVerdict.Pass, Score: null, Reasons: []);

    private static GateResult Spent { get; } = new(GateVerdict.Fail, Score: null,
        Reasons: ["the item's token budget is spent; grant tokens with 'sindri workflow answer <instance> --grant-tokens <n>' at a terminal"]);
}
