using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Traits;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>Reads Jev's answers into findings, and logs each assessment (the log tunes the hook's thresholds).</summary>
/// <param name="log">The trait-check log.</param>
internal sealed class TraitVerdicts(ITraitCheckLog log)
{
    internal CheckTraitsResponse Read(CheckTraitsRequest request, double minimum, TraitPlan plan, IReadOnlyList<ProviderResult> answers)
    {
        var mode = request.Mode.ToString().ToLowerInvariant();
        List<TraitFinding> findings = [];
        List<string> failures = [];
        var skipped = new Dictionary<string, string>(plan.Skipped, StringComparer.Ordinal);
        var left = 0;
        for (var i = 0; i < answers.Count; i++)
        {
            // An image Jev was not sent: its answer on the file's name alone is never used. The file is skipped, on its own row.
            if (plan.Tasks[i].Images.Count > 0 && answers[i].ImageNotes is { Count: > 0 } notes)
            {
                skipped[plan.Display(plan.Tasks[i])] = CheckTraitsResponse.ImageNotSent;
                log.Append(new JsonObject { ["event"] = "image-not-sent", ["mode"] = mode, ["repo"] = plan.Tasks[i].Root, ["path"] = plan.Tasks[i].Source.Path, ["reason"] = string.Join("; ", notes) });
                left++;
                continue;
            }

            if (answers[i] is not BatchDecided decided)
            {
                log.Append(new JsonObject { ["event"] = "jev-unavailable", ["mode"] = mode, ["reason"] = answers[i].ReasonCode });
                failures.Add(answers[i].ReasonCode);
                continue;
            }

            findings.AddRange(Assess(request.Mode, minimum, plan, plan.Tasks[i], decided));
        }

        return failures.Count > 0 && failures.Count == answers.Count - left
            ? new CheckTraitsResponse([], JevUnavailable: true, Reason: failures[0], Requests: answers.Count) { Skipped = skipped.Count == 0 ? CheckTraitsResponse.NoSkips : skipped }
            : new CheckTraitsResponse([.. findings.OrderByDescending(finding => finding.Probability)], Requests: answers.Count) { Skipped = skipped.Count == 0 ? CheckTraitsResponse.NoSkips : skipped };
    }

    private IEnumerable<TraitFinding> Assess(TraitCheckMode mode, double minimum, TraitPlan plan, TraitTask task, BatchDecided decided)
    {
        foreach (var trait in task.Batch)
        {
            if (!decided.Answers.TryGetValue(trait.Name, out var raw) || raw is not ChoiceBatchAnswer choice)
            {
                continue;
            }

            var p = Violation(choice);
            var reported = p >= minimum && (mode != TraitCheckMode.Stop || trait.Severity == TraitSeverity.Block);

            // A scan's thousands of assessments would drown the log.
            if (mode != TraitCheckMode.Scan)
            {
                log.Append(new JsonObject
                {
                    ["event"] = "assessment",
                    ["mode"] = mode.ToString().ToLowerInvariant(),
                    ["trait"] = trait.Name,
                    ["repo"] = task.Root,
                    ["path"] = task.Source.Path,
                    ["lines"] = new JsonArray(task.Source.StartLine, task.Source.EndLine),
                    ["choice"] = choice.Choice,
                    ["p"] = p,
                    ["reported"] = reported,
                });
            }

            if (reported)
            {
                yield return Finding(trait, plan, task, p);
            }
        }
    }

    // The probability of a violation: the model's own, else its confidence when it chose it.
    private static double Violation(ChoiceBatchAnswer choice) =>
        choice.Probabilities.TryGetValue(TraitQuestions.Violation, out var probability)
            ? probability
            : string.Equals(choice.Choice, TraitQuestions.Violation, StringComparison.Ordinal) ? choice.Confidence : 0;

    // The file and the trait file are named for the agent: relative in the working directory's repo, absolute in another.
    private static TraitFinding Finding(Trait trait, TraitPlan plan, TraitTask task, double p) => new(
        trait.Name,
        trait.Severity == TraitSeverity.Block ? "block" : "warn",
        plan.Display(task),
        p,
        trait.Requirement.Trim(),
        task.Source.StartLine,
        task.Source.EndLine,
        trait.Guidance?.Trim(),
        string.Equals(task.Root, plan.Home, StringComparison.Ordinal) && trait.File.StartsWith(task.Root, StringComparison.Ordinal) ? Path.GetRelativePath(task.Root, trait.File) : trait.File);
}
