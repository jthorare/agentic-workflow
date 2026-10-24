using System.Text.RegularExpressions;

namespace Aw.Judge.Core.PromptSort;

/// <summary>
/// Axes to scaffolds. A scaffold fires only when Jev decided its driving axes (a heuristic-only run fires nothing: the
/// heuristic is the baseline under test), its switch is on, the prompt doesn't already name a workflow, the user didn't
/// ask for a loop (brief and plan-first) and its cooldown has passed. Switched-off scaffolds are still reported as
/// 'would fire', for the eval.
/// </summary>
public static partial class ScaffoldPlanner
{
    private const string Header = "Prompt sorter (automatic, may be wrong; skip any note that does not fit):";

    /// <summary>Plans the scaffolds for one sorted prompt.</summary>
    /// <param name="outcome">What the sort found.</param>
    /// <param name="settings">The scaffold switches and the cooldown.</param>
    /// <param name="session">The session's state before this prompt.</param>
    /// <param name="prompt">The prompt as typed (a prompt that names a workflow is never scaffolded).</param>
    /// <returns>The plan.</returns>
    public static ScaffoldPlan Plan(SortOutcome outcome, PromptSortSettings settings, SortSession session, string prompt)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(prompt);
        var namesWorkflow = NamesWorkflow(prompt);
        var verdicts = ScaffoldCatalog.All
            .Where(s => s.Applies(outcome.Values))
            .Select(s => (Scaffold: s, Verdict: Decide(s, outcome, settings, session, namesWorkflow)))
            .ToList();
        var fire = verdicts.Where(static v => v.Verdict.Held is null).Select(static v => v.Scaffold).ToList();
        return new ScaffoldPlan(
            [.. verdicts.Where(static v => v.Verdict.WouldFire).Select(static v => v.Scaffold.Id)],
            [.. fire.Select(static s => s.Id)],
            [.. verdicts.Where(static v => v.Verdict.Held is not null).Select(static v => Suppression(v.Scaffold.Id, v.Verdict.Held!))],   // why: filtered to the held ones
            verdicts.Exists(static v => v.Verdict.SetsUiRequirement),
            Context(fire));
    }

    /// <summary>Whether a prompt already names a workflow, so needs no pointer to it.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>True when it does.</returns>
    public static bool NamesWorkflow(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return Workflow.IsMatch(prompt);
    }

    // The checks run in this order; the first that holds a scaffold back is its reason.
    private static Verdict Decide(Scaffold scaffold, SortOutcome outcome, PromptSortSettings settings, SortSession session, bool namesWorkflow)
    {
        if (!scaffold.NeedsJudge.All(outcome.Judged))
        {
            return new Verdict(WouldFire: false, Held: "heuristic-only", SetsUiRequirement: false);
        }

        if (!settings.Scaffolds.GetValueOrDefault(scaffold.Id))
        {
            return new Verdict(WouldFire: true, Held: "switch-off", SetsUiRequirement: false);
        }

        if (namesWorkflow)
        {
            return new Verdict(WouldFire: true, Held: "already-named", SetsUiRequirement: false);
        }

        // The done gate's requirement outlives the note's cooldown and a loop request.
        return new Verdict(WouldFire: true, Held: Hold(scaffold, outcome, settings, session), SetsUiRequirement: scaffold.RequiresUiEvidence);
    }

    private static string? Hold(Scaffold scaffold, SortOutcome outcome, PromptSortSettings settings, SortSession session)
    {
        if (outcome.Values.Flag(SortAxes.WantsLoop) && (string.Equals(scaffold.Id, "brief", StringComparison.Ordinal) || string.Equals(scaffold.Id, "plan-first", StringComparison.Ordinal)))
        {
            return "loop-requested";
        }

        return session.LastFired.TryGetValue(scaffold.Id, out var last) && session.Prompts + 1 - last < settings.CooldownPrompts ? "cooldown" : null;
    }

    private static string Context(List<Scaffold> fire) =>
        fire.Count == 0 ? "" : string.Join('\n', new[] { Header }.Concat(fire.Select(static s => $"- {s.Text}")));

    private static string Suppression(string id, string reason) => $"{{\"id\":\"{id}\",\"reason\":\"{reason}\"}}";

    [GeneratedRegex(@"\b(bugFixOrchestrator|rootCause|bugHunt)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex Workflow { get; }

    private sealed record Verdict(bool WouldFire, string? Held, bool SetsUiRequirement);
}
