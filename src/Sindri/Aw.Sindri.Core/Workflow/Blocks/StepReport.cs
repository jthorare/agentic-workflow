using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What a run reports (spec §2, W20): an outcome and outputs, from the agent or the desk.</summary>
/// <param name="Outcome">One of the Step's declared outcomes.</param>
/// <param name="Outputs">The facts it wrote.</param>
public sealed record StepReport(OutcomeName Outcome, IReadOnlyDictionary<FactKey, JsonElement> Outputs);
