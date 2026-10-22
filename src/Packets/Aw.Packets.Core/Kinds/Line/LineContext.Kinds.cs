using System.Text.RegularExpressions;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Kinds.Line;

internal sealed partial class LineContext
{
    [GeneratedRegex("^(?<base>[a-z0-9]+(?:-[a-z0-9]+)+?)(?:-a(?<n>[1-9][0-9]*))?\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhaseIdShape { get; }

    internal TaskPlan? PlanTasks() =>
        Schema<TaskPlan>(_rules.Number(LineRules.TasksSchema), "tasks.json", "task-plan", "tasks.json matches the task-plan schema", static plan => LineProblems.Tasks(plan));

    /// <summary>Runs the rules only this context's kind has.</summary>
    /// <returns>The plan graph's task plan when it is one that matches its schema; null for any other kind or outcome.</returns>
    internal TaskPlan? KindRules()
    {
        switch (_kind.Name)
        {
            case "design":
                Acceptance(LineRules.AcceptanceSchema, allowPassing: false);
                break;
            case "proof":
                Acceptance(LineRules.AcceptanceSchema, allowPassing: true);
                break;
            case "handoff":
                _ = Schema<PullRequestFile>(_rules.Number(LineRules.PullRequestSchema), "pr.json", "pull-request", "pr.json matches its schema", LineProblems.PullRequest);
                break;
            case "phase":
                Phase();
                break;
            case "plan":
                return PlanTasks();
            default:
                break;
        }

        return null;
    }

    private void Phase()
    {
        PhaseId();
        var phase = Schema<PhaseFile>(_rules.Number(LineRules.PhaseSchema), "phase.json", "phase", "phase.json matches its schema", LineProblems.Phase);
        AmendsRule(phase);
        Acceptance(LineRules.PhaseAcceptanceSchema, allowPassing: false);
        HoldoutSealRule(Schema<PhaseHoldout>(_rules.Number(LineRules.PhaseHoldout), "holdout.json", "phase-holdout", "holdout.json matches its schema", LineProblems.Holdout));
    }

    // PKT-HOLD-003: the set the evaluator runs is the set that was sealed with the plan.
    private void HoldoutSealRule(PhaseHoldout? holdout)
    {
        if (holdout is null)
        {
            _checks.Unchecked(PacketRules.HoldoutSealed, "holdout.json is missing or doesn't match its schema, so its set hash is unknown");
            return;
        }

        if (SealedSet is not { } sealedSet)
        {
            _checks.Unchecked(PacketRules.HoldoutSealed, "no seal file was given (--seal), so the set hash was not compared with a seal");
            return;
        }

        _checks.Rule(
            PacketRules.HoldoutSealed,
            "the holdout set hash matches its seal",
            holdout.SetHash == sealedSet
                ? []
                : [("holdout.json setHash", $"setHash {holdout.SetHash.Value} is not the sealed set {sealedSet.Value}", "Re-freeze the phase from the sealed set, or re-seal the set and the plan.")]);
    }

    private void PhaseId()
    {
        var rule = _rules.Number(LineRules.PhaseId);
        if (Manifest is not { } manifest)
        {
            _checks.Unchecked(rule, "packet.json is missing or doesn't match its schema, so the packet id can't be checked");
            return;
        }

        _checks.Rule(
            rule,
            "the packet id is <line>-<phase>[-a<n>]",
            PhaseIdShape.IsMatch(manifest.Id.Value)
                ? []
                : [("packet", $"id '{manifest.Id.Value}' is not <line>-<phase> or <line>-<phase>-a<n> (lower-case letters, digits and '-', at least a line and a phase, an amendment number from 1).", "Name the packet <line>-<phase>, and <line>-<phase>-a<n> for the nth amendment. Sindri keeps ids unique per line.")]);
    }

    private void AmendsRule(PhaseFile? phase)
    {
        var rule = _rules.Number(LineRules.PhaseAmends);
        if (Manifest is not { } manifest || phase is null)
        {
            _checks.Unchecked(rule, "packet.json or phase.json can't be read, so amendments can't be checked");
            return;
        }

        var amendment = PhaseIdShape.Match(manifest.Id.Value) is { Success: true } match && match.Groups["n"].Success;
        _checks.Rule(rule, "phase.json has 'amends' exactly when the id ends -a<n>", LineProblems.Amends(manifest, phase, amendment).Select(static p => ("file phase.json", p.Message, p.Fix)));
    }

    private void Acceptance(int schemaRule, bool allowPassing)
    {
        var schema = _rules.Number(schemaRule);
        var chain = _rules.Number(schemaRule + 1);
        var file = Schema<AcceptanceFile>(schema, "acceptance.json", "acceptance", "acceptance.json matches its schema", static a => LineProblems.AcceptanceShape(a));
        if (file is null)
        {
            _checks.Unchecked(chain, "acceptance.json doesn't match its schema, so its chain rules can't be checked");
            return;
        }

        _checks.Rule(
            chain,
            allowPassing ? "item ids are unique and every passing item has evidence" : "item ids are unique and no item is passing yet",
            LineProblems.Chain(file, allowPassing).Select(static p => ($"acceptance.json {p.Where}", p.Message, p.Fix)));
    }

}
