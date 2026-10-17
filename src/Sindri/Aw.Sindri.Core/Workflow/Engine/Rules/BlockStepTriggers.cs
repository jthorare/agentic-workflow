using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>The triggers a block-runtime Step's result becomes (DT spec §8.2): the engine reports for the Step, and a result no desk report could have been is a failure, not a refusal.</summary>
public static class BlockStepTriggers
{
    /// <summary>The actor a block-runtime Step reports as.</summary>
    /// <param name="step">The Step's id.</param>
    /// <returns>'service:block:&lt;step&gt;'.</returns>
    public static string Actor(BlockId step)
        => "service:block:" + step.Value;

    /// <summary>
    /// A completed result as a trigger: a <see cref="StepCompleted"/> when the report validation of WF §5.3 accepts it, else a
    /// <see cref="StepFailed"/> with the reason 'invalid-result: &lt;code&gt;' so the run can't hang on a refusal nobody sees.
    /// </summary>
    /// <param name="instance">The instance with the run open.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="context">The runtimes and the catalog.</param>
    /// <param name="result">What the Step reported.</param>
    /// <returns>The trigger to decide.</returns>
    public static Trigger Completed(WorkflowInstance instance, Recipe recipe, DecisionContext context, BlockCompletion result)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        var report = new StepCompleted(Actor(result.Step), result.Run, result.Outcome, result.Outputs, Tokens: 0, StepRuntime.Block, Sha(result), result.Decision);
        return TransitionDecision.Decide(new DecisionInput(instance, recipe, report, GateAnswers.None, context)) is Rejected rejected
            ? Failed(result.Step, result.Run, $"invalid-result: {rejected.Error.Code} {rejected.Error.Message}")
            : report;
    }

    /// <summary>A failed run as a trigger.</summary>
    /// <param name="step">The Step.</param>
    /// <param name="run">The open run.</param>
    /// <param name="reason">What failed.</param>
    /// <returns>The step.failed trigger.</returns>
    public static StepFailed Failed(BlockId step, RunToken run, string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return new StepFailed(Actor(step), run, reason, Tokens: 0, Hash("failed\n" + reason));
    }

    // The canonical report: the outcome and each output's content digest, in key order, so two different results never share a trigger key.
    private static string Sha(BlockCompletion result) =>
        Hash(result.Outcome.Value + "\n" + string.Join('\n', result.Outputs.OrderBy(static o => o.Key.Value, StringComparer.Ordinal).Select(static o => o.Key.Value + "=" + o.Value.Sha256)));

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
