using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The gate seam (engine P7 plan D7, spec §5.2): before the loop decides a trigger it asks which transition the trigger
/// matches (<see cref="MatchedTransition"/>) and, when that transition has gates, runs them in the 'EvaluateGates' local
/// activity, then decides with their answers. A history from before engine P7 (no 'p7-children' marker) keeps deciding with
/// no answers, as recorded.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private static readonly LocalActivityOptions _gateOptions = new()
    {
        StartToCloseTimeout = ActivityBounds.Long,
        RetryPolicy = new Temporalio.Common.RetryPolicy { MaximumAttempts = 3 },
    };

    /// <summary>The input of one decision: <paramref name="trigger"/> on <paramref name="instance"/> with the answers of the gates its transition runs.</summary>
    private async Task<DecisionInput> Deciding(WorkflowInstance instance, Interpreting context, Trigger trigger) =>
        new(instance, context.Recipe, trigger, await GateAnswersOf(instance, context, trigger).ConfigureAwait(true), context.Decisions);

    private async Task<GateAnswers> GateAnswersOf(WorkflowInstance instance, Interpreting context, Trigger trigger)
    {
        if (!_children || MatchedTransition.Of(instance, context.Recipe, trigger) is not { Gates.Count: > 0 } transition)
        {
            return GateAnswers.None;
        }

        var run = new GateRun(instance.Address.Value, transition.Gates, instance.Facts, ItemBudget.TokensLeft(instance, context.Recipe));
        try
        {
            var answered = await Workflow.ExecuteLocalActivityAsync((GateActivities a) => a.EvaluateGatesAsync(run), _gateOptions).ConfigureAwait(true);
            return new GateAnswers(answered.ToDictionary(static a => a.Gate, static a => a.Result), GateAnswers.None.Evidence);
        }
        catch (Exception failed) when (IsActivityFailure(failed))
        {
            return GateAnswers.None;   // boundary: the activity ran out of attempts; no answer is a refusal as unavailable (W5), never a stuck workflow
        }
    }
}
