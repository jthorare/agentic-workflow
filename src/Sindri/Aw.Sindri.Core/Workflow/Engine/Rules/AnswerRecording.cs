using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// 'workflow answer --record' (#1270): a desk-free answer batch goes in through the open run of the 'step.interview.answer' desk Step, so
/// 'step.interview.record' runs as normal. This is the rule that finds that run, or refuses with the fix.
/// </summary>
public static class AnswerRecording
{
    private const string InterviewAnswer = "step.interview.answer";

    /// <summary>The open interview desk run's token, or the refusal (SND-WF-003, "rewind to interview first") for an instance that isn't at it.</summary>
    /// <param name="instance">The instance as it stands.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The run token to complete as 'answered', or the refusal.</returns>
    public static Result<RunToken> InterviewRun(WorkflowInstance instance, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        var address = instance.Address.Value;
        var fix = $"rewind to interview first: sindri workflow rewind {address} --to interview";
        if (instance.Ended || instance.Suspension is not null)
        {
            return WorkflowErrors.TriggerNotAllowed.With($"answer --record needs {address} at 'interview' with its desk run open, and it is {(instance.Ended ? "ended" : "parked or taken over")} (fix: {fix})");
        }

        var step = recipe.States.TryGetValue(instance.Frame.State, out var state) ? state.Step : null;
        return string.Equals(step?.Use.Value, InterviewAnswer, StringComparison.Ordinal) && instance.Frame.Run is { Runtime: StepRuntime.Desk } run
            ? run.Token
            : WorkflowErrors.TriggerNotAllowed.With($"answer --record needs {address} at 'interview', not '{instance.State}' (fix: {fix})");
    }

    /// <summary>The same rule over the instance detail the CLI reads: the open desk run of 'step.interview.answer' when the instance is in that state and not parked or taken over, else the refusal.</summary>
    /// <param name="detail">The instance detail ('GET /workflow/instances/{id}').</param>
    /// <returns>The run token to complete as 'answered', or the refusal.</returns>
    public static Result<RunToken> InterviewRun(InstanceDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        var address = detail.Address.Value;
        var fix = $"rewind to interview first: sindri workflow rewind {address} --to interview";
        return string.Equals(detail.State, detail.Current.State, StringComparison.Ordinal) && string.Equals(detail.Current.Step?.Ref, InterviewAnswer, StringComparison.Ordinal) && detail.Current.Desk is { } desk
            ? desk.Run
            : WorkflowErrors.TriggerNotAllowed.With($"answer --record needs {address} at 'interview' with its desk run open, not '{detail.State}' (fix: {fix})");
    }
}
