using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>'next' (engine spec §3.7, §3.8, §6.4).</summary>
internal sealed partial record InstanceWords
{
    /// <summary>
    /// The rendered 'next': a suspension's §3.8 text (a park puts its instruction on a line of its own), a desk task's
    /// recipe text followed by the 'workflow complete' line, a wait's recipe text, and nothing once the instance ended.
    /// </summary>
    /// <param name="task">The open desk task, if any.</param>
    /// <returns>The text; '\n' separates its lines.</returns>
    public string Next(DeskTask? task) => Instance switch
    {
        { Ended: true } => string.Empty,
        { Suspension: { Kind: SuspensionKind.Park } park } => Parked(park),
        { Suspension: { Kind: SuspensionKind.Escalation } escalation } => Escalated(escalation),
        { Suspension.Kind: SuspensionKind.Takeover } =>
            $"With you since {Since.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture)}. Run `sindri workflow handback {Address}` when you're done.",
        _ when task is not null => $"{BeforeRun(RecipeNext())}, then run:\n{Complete(task)}",
        _ => RecipeNext(),
    };

    /// <summary>The recipe's desk text without its own closing 'then complete this Step', which the 'then run:' line that follows says.</summary>
    private static string BeforeRun(string text)
    {
        var trimmed = text.TrimEnd('.');
        var closing = trimmed.LastIndexOf(", then complete this Step", StringComparison.OrdinalIgnoreCase);
        return closing >= 0 && closing + ", then complete this Step".Length == trimmed.Length ? trimmed[..closing] : trimmed;
    }

    private string Parked(Suspension park)
    {
        var question = park.Question.TrimEnd('.');
        var where = park.Mirrors is null ? string.Empty : $" in {ChildName}";
        return park.Reason switch
        {
            ParkReason.Rounds or ParkReason.NoProgress => $"Parked ({Name(park.Reason.Value)}){where}: {question}.\n{RoundsAnswer(park)}",
            ParkReason.Budget => $"Parked (budget){where}: {question}.\nGrant tokens at a terminal, or cancel.",
            ParkReason.NeedsAnswer when (park.ParkKind ?? park.MirroredParkKind) == ParkKind.Wait => $"Parked{where}: {question}.\nFix the cause, then resume to check again, or cancel.",
            ParkReason.NeedsAnswer => $"Parked (needs-answer){where}: {question}.\nAnswer with --text; the Step runs again with your answer.",
            ParkReason.Timeout => $"Parked (timeout): {question}.\nResume to give it the full time again, or cancel.",
            ParkReason.Unavailable => $"Parked (unavailable){where}: {question}.\nFix the dependency, then resume to run the Step again, or cancel.",
            ParkReason.SpecConflict when park.Mirrors is not null => $"Parked (spec-conflict){where}: {question}.",
            ParkReason.SpecConflict => SpecConflict(park),
            ParkReason.NoRuntime or null or _ => $"No runtime for {Block(question)} in this build.\nCancel, or resume in a build that has it.",
        };
    }

    /// <summary>The way out of a rounds or no-progress park: a grant, and when the run can go back to a step, the rewind that re-runs it on the facts the run holds (#1270).</summary>
    private string RoundsAnswer(Suspension park)
    {
        var targets = TransitionDecision.RewindTargets(Recipe.Recipe, Recipe.Catalog, Instance);
        if (targets.Count == 0)
        {
            return "Grant more rounds at a terminal, or cancel.";
        }

        var withheld = park.Resume.Withheld?.Transition is { } index && index >= 0 && index < Recipe.Recipe.Transitions.Count && Recipe.Recipe.Transitions[index].From.Contains(targets[0], StringComparer.Ordinal);
        var rewind = withheld
            ? $"rewind to re-run {targets[0]} on the same facts (`sindri workflow rewind {Address} --to {targets[0]}`)"
            : $"rewind to re-run an earlier step on the same facts (`sindri workflow rewind {Address} --to <state>`, one of: {string.Join(", ", targets)})";
        return $"Grant more rounds at a terminal, {rewind}, or cancel.";
    }

    private string SpecConflict(Suspension park)
    {
        var id = Recipe.Version.Id.Value;
        return park.ParkKind switch
        {
            ParkKind.Revoked => $"{id} was revoked.\nCancel, then start a new attempt from an approved version.",
            ParkKind.ChildRevoked => $"{ChildRecipe(park.Question)} (pinned by {id}) was revoked.\nRe-approve {id} so it pins an approved child; the approval resumes this instance.",
            ParkKind.Drift => $"{Block(park.Question)} changed since {Pinned} was approved.\nRe-approve it (`sindri recipe approve {Pinned}`); the approval resumes this instance.",
            ParkKind.Dispatch or ParkKind.Loop or ParkKind.Refusal or ParkKind.Step or ParkKind.Wait or null or _ => park.Question,   // why: the engine wrote the reason with its own fix (SND-WF-010/015 or the allowlist sentence of SND-WF-011), so it is said once
        };
    }

    private string Escalated(Suspension escalation)
    {
        var question = escalation.Question.TrimEnd('.');
        return escalation.Escalation switch
        {
            EscalationKind.Gate =>
                $"Escalated: {question}. Accept to override it (the other checks still run before {Target(escalation.Resume)}), or reject (the Step re-runs within its loop, otherwise this parks).",
            EscalationKind.Timeout => $"Escalated: {question}. Resume to give it the full time again, or cancel.",
            EscalationKind.Child or null or _ => $"Escalated in {ChildName}: {question}",
        };
    }

    /// <summary>The state an escalated gate guards: the withheld transition's target.</summary>
    private string Target(ResumeAction resume) =>
        resume.Withheld?.Transition is { } index && index >= 0 && index < Recipe.Recipe.Transitions.Count
            ? Recipe.Recipe.Transitions[index].To
            : "the next state";

    /// <summary>The 'workflow complete' line for the task: its outcome (all of them when there are several) and the outputs required on a single one.</summary>
    private string Complete(DeskTask task) =>
        CompleteCommand.Line(Address, task.Run, task.Outcomes, task.Outputs);

    /// <summary>The state's recipe 'next' with its variables filled in (§3.7); one the engine can't fill stays as written.</summary>
    private string RecipeNext() =>
        Recipe.Recipe.States.TryGetValue(Instance.Frame.State, out var state) && state.Next is { } text
            ? RecipeNextText.Fill(text, Variable)
            : string.Empty;

    private string? Variable(string name) => name switch
    {
        "item" => Instance.Item.Value,
        "instance" => Address,
        "state" => Instance.Frame.State,
        "child" => Child?.Address.Value,
        "budget.tokens.left" => TokensLeft(),
        _ when name.StartsWith("loop.", StringComparison.Ordinal) => Loop(name["loop.".Length..]),
        _ => null,
    };

    /// <summary>'{loop.n}' is the round the loop is on (1 before it has counted one); '{loop.n.cap}' its cap.</summary>
    private string? Loop(string rest)
    {
        var cap = rest.EndsWith(".cap", StringComparison.Ordinal);
        var name = cap ? rest[..^".cap".Length] : rest;
        if (!Recipe.Recipe.Loops.TryGetValue(name, out var spec))
        {
            return null;
        }

        var round = Instance.Loops.TryGetValue(name, out var state) ? state.Round : 1;
        return (cap ? spec.Cap : round).ToString(CultureInfo.InvariantCulture);
    }

    private string? TokensLeft()
    {
        var cap = Recipe.Recipe.States.TryGetValue(Instance.Frame.State, out var state) && state.Budget is { } own ? own.Tokens : Recipe.Recipe.Budget?.Tokens;
        var spent = state?.Budget is not null ? BudgetRules.StateUsed(Instance, Recipe.Recipe, Instance.Frame.State) : Instance.Spent.Total;
        return cap is { } tokens ? StatusText.Tokens(Math.Max(0, tokens - spent)) : null;
    }

    private static string ChildRecipe(string question) =>
        RecipeId.TryFrom(question.TrimEnd('.'), out var id) ? id.Value : "A child recipe";
}
