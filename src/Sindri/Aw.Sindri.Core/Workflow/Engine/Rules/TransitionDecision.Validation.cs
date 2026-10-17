using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The checks a decision can see (spec §5.2, the Update validator; §5.3): the run token and the state (SND-WF-005,
/// SND-WF-003), a malformed report (SND-WF-008) and the approval (RCP-CAP-003). A validation refusal records and
/// consumes nothing.
/// </summary>
public static partial class TransitionDecision
{
    private static Error? Invalid(Deciding d, WorkflowInstance instance, TransitionMatch match) => match.Miss switch
    {
        MatchMiss.StaleRun => WorkflowErrors.StaleRunToken.With(
            $"run {Run(d.Trigger)?.Value} isn't the open run{(instance.Frame.Run is { } open ? $" {open.Token.Value}" : string.Empty)} of '{instance.State}' (fix: use the token in status)"),
        MatchMiss.NotAllowed => NotAllowed(d.Trigger, instance),
        MatchMiss.EngineOwnedRun => WorkflowErrors.TriggerNotAllowed.With(
            $"the engine is running this Step; take it over to answer it yourself (fix: sindri workflow takeover {instance.Address.Value}, then complete the run)"),
        MatchMiss.None or MatchMiss.NoTransition or MatchMiss.EngineTrigger or _ => d.Trigger is StepCompleted completed ? Malformed(d, instance, completed) : null,
    };

    private static Error NotAllowed(Trigger trigger, WorkflowInstance instance) => WorkflowErrors.TriggerNotAllowed.With(
        $"{trigger.Name} isn't allowed while {instance.Address.Value} is in '{instance.State}' (fix: see allowedCommands in status)");

    /// <summary>A report with an undeclared outcome, a missing required output or an undeclared fact (§5.3): rejected before any decision, so no round is used.</summary>
    private static Error? Malformed(Deciding d, WorkflowInstance instance, StepCompleted report)
    {
        var use = d.Recipe.States[instance.State].Step;
        if (use is null || d.Context.Catalog.Find(use.Use) is not { } descriptor)
        {
            return WorkflowErrors.MalformedReport.With($"the Step of '{instance.State}' isn't in this build's catalog (fix: run it in a build that has it)");
        }

        var step = use.Use;
        if (StepOutcomes.Of(descriptor, use, d.Recipe) is not { } outcomes)
        {
            return WorkflowErrors.MalformedReport.With($"the choices of '{instance.State}' don't resolve to outcomes (fix: sindri recipe validate the recipe)");
        }

        if (!outcomes.Contains(report.Outcome))
        {
            return WorkflowErrors.MalformedReport.With(
                $"{step.Value} reported outcome '{report.Outcome.Value}', which it doesn't declare (fix: use one of {Names(outcomes.Select(o => o.Value))})");
        }

        var missing = descriptor.Outputs.FirstOrDefault(o => StepOutputs.RequiredOn(o, report.Outcome) && !report.Outputs.ContainsKey(o.Fact));
        if (missing is not null)
        {
            var fix = missing.EngineFilled == true
                ? $"the engine writes {missing.Fact.Value} from --outcome: retry 'workflow complete' with the same --nonce"   // CompleteStep refuses a hand-written one
                : $"include the required output {missing.Fact.Value}";
            return WorkflowErrors.MalformedReport.With($"{step.Value} reported '{report.Outcome.Value}' without {missing.Fact.Value} (fix: {fix})");
        }

        var undeclared = report.Outputs.Keys.Where(k => !descriptor.Outputs.Any(o => o.Fact == k)).Select(k => k.Value).Order(StringComparer.Ordinal).ToList();
        return undeclared.Count == 0
            ? null
            : WorkflowErrors.MalformedReport.With(
                $"{step.Value} reported {Names(undeclared)}, which it doesn't declare (fix: report only {Names(descriptor.Outputs.Select(o => o.Fact.Value))})");
    }

    /// <summary>
    /// The approval check (§5.2, W11): a human trigger on a revoked or drifted recipe is SND-RECIPE-005 or -006 and
    /// records nothing; a system trigger parks with spec-conflict instead. Cancel, takeover, handback and takeoverIdle
    /// are exempt.
    /// </summary>
    private static Decision? Unapproved(Deciding d, WorkflowInstance instance, int? transition)
    {
        var approval = d.Context.Approval;
        if (approval == ApprovalCheck.Holds || d.Trigger.ExemptFromApproval)
        {
            return null;
        }

        if (!IsSystem(d.Trigger.Actor))
        {
            return new Rejected(d.Key, ApprovalError(approval, d.Recipe));
        }

        var revoked = approval == ApprovalCheck.Revoked;
        var question = revoked
            ? $"{d.Recipe.Id.Value} was revoked. Cancel, then start a new attempt from an approved version."
            : $"A block pinned by {d.Recipe.Id.Value} changed since it was approved. Re-approve it; the approval resumes this instance.";
        var withheld = d.Trigger is StepCompleted or StepFailed or WaitSatisfied or CommandFired ? new WithheldTrigger(transition, d.Trigger) : null;
        var park = new Suspension(
            SuspensionKind.Park, ParkReason.SpecConflict, revoked ? ParkKind.Revoked : ParkKind.Drift, Escalation: null, question, d.Trigger.Actor, Mirrors: null,
            new ResumeAction(revoked ? ResumeKind.None : ResumeKind.Rearm, revoked ? null : withheld, Gate: null, Effects: []));
        return d.ChangedOrAbsorbed(d.Rules.Suspend(instance, park), instance, "already parked for drift");
    }

    private static Error ApprovalError(ApprovalCheck approval, Recipe recipe) => approval == ApprovalCheck.Revoked
        ? WorkflowErrors.RecipeRevoked.With($"{recipe.Id.Value} version {recipe.Version.ToString(CultureInfo.InvariantCulture)} was revoked (fix: approve a new version)")
        : WorkflowErrors.RecipePinDrift.With($"a block pinned by {recipe.Id.Value} changed since it was approved (fix: re-approve it; sindri recipe list --stale)");

    /// <summary>System triggers carry 'service:' or 'system:' actors (§5.2); everything else came from a person.</summary>
    private static bool IsSystem(string actor) =>
        actor.StartsWith("service:", StringComparison.Ordinal) || actor.StartsWith("system:", StringComparison.Ordinal);

    /// <summary>A Step run's spend (§3.7): the new spend, and a budget park when it ran out.</summary>
    private static BudgetVerdict Spend(Deciding d, WorkflowInstance instance) => d.Trigger switch
    {
        StepCompleted completed => d.Rules.Spend(instance, d.Recipe, completed.Tokens),
        StepFailed failed => d.Rules.Spend(instance, d.Recipe, failed.Tokens),
        _ => new BudgetVerdict(instance.Spent, Park: null),
    };

    private static string Names(IEnumerable<string> names) => string.Join(", ", names);
}
