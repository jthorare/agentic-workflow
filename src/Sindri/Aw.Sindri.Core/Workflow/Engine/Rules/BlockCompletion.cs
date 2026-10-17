using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>What a block-runtime Step reported (DT spec §8.2).</summary>
/// <param name="Step">The Step's id.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Outcome">The reported outcome.</param>
/// <param name="Outputs">The stored outputs.</param>
/// <param name="Decision">The Judge decision id the Step reported, or null.</param>
public sealed record BlockCompletion(BlockId Step, RunToken Run, OutcomeName Outcome, IReadOnlyDictionary<FactKey, FactValue> Outputs, string? Decision = null);
