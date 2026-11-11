using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>Everything the validator consults besides the document.</summary>
/// <param name="Blocks">The block types by name.</param>
/// <param name="World">Repos, connections and memory stores.</param>
/// <param name="CronProblem">Why a cron text and timezone don't parse, or null when they do (the cron reader, wired after PR 5's parts merge).</param>
/// <param name="Condition">Parses a condition text (the expression language, wired after PR 5's parts merge).</param>
public sealed record GraphValidationContext(
    IReadOnlyDictionary<string, IGraphBlockType> Blocks,
    IGraphWorld World,
    Func<string, string?, string?> CronProblem,
    Func<string, ConditionParse> Condition)
{
    /// <summary>Gets the box's cap on a sandbox's memory and wall clock, or null to skip the check (PR 14).</summary>
    public ResourceBoxCap? BoxCap { get; init; }

    /// <summary>Compiles a 'code' node's body and lists its errors, or null to skip the check (agent-first PR 9c).</summary>
    public Func<string, IReadOnlyList<CodeProblem>>? CodeCheck { get; init; }
}
