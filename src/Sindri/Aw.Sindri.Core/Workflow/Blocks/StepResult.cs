using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A completed run's result (spec §2): the trigger's outcome and the facts it records.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Outputs">The facts.</param>
public sealed record StepResult(OutcomeName Outcome, IReadOnlyDictionary<FactKey, JsonElement> Outputs);
