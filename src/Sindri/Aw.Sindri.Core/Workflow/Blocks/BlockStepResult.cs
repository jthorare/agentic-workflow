using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What a block-runtime Step returns (DT spec §8.2): an outcome with its facts, or a failure the engine records as step.failed.</summary>
/// <param name="Result">The completed result; null when the Step failed.</param>
/// <param name="Failure">Why it failed, in the words the human reads; null when it completed.</param>
public sealed record BlockStepResult(StepResult? Result, string? Failure)
{
    /// <summary>A completed run.</summary>
    /// <param name="outcome">One of the Step's declared outcomes.</param>
    /// <param name="outputs">The facts it records.</param>
    /// <returns>The result.</returns>
    public static BlockStepResult Completed(OutcomeName outcome, IReadOnlyDictionary<FactKey, JsonElement> outputs) => new(new StepResult(outcome, outputs), Failure: null);

    /// <summary>A failed run; the engine records step.failed with <paramref name="reason"/>.</summary>
    /// <param name="reason">What failed.</param>
    /// <returns>The result.</returns>
    public static BlockStepResult Failed(string reason) => new(Result: null, reason);
}
