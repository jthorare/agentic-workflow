using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What a gate evaluates (spec §2). The engine (P3) fills it; later phases may add members.</summary>
/// <param name="Gate">The gate's id.</param>
/// <param name="Arguments">Its 'with:' arguments, resolved to values.</param>
/// <param name="Facts">The instance's facts.</param>
/// <param name="Threshold">The recipe's threshold for a rung-2 or rung-3 gate.</param>
public sealed record GateContext(
    BlockId Gate,
    IReadOnlyDictionary<string, JsonElement> Arguments,
    IReadOnlyDictionary<FactKey, JsonElement> Facts,
    double? Threshold)
{
    /// <summary>
    /// The tokens the item may still spend: the profile budget less what the instance spent, raised by
    /// '--grant-tokens' (spec §5.8, §13 arch N7). Null when no budget applies. 'gate.budget.available' reads it.
    /// </summary>
    public int? TokensLeft { get; init; }
}
