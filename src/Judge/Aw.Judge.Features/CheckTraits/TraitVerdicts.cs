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
        for (var i = 0; i < answers.Count; i++)
        {
            if (answers[i] is not BatchDecided decided)
            {
                log.Append(new JsonObject { ["event"] = "jev-unavailable", ["mode"] = mode, ["reason"] = answers[i].ReasonCode });
                failures.Add(answers[i].ReasonCode);
                continue;
            }

            findings.AddRange(Assess(request.Mode, minimum, plan.Tasks[i], decided, plan.Root));
        }

        return failures.Count == answers.Count
            ? new CheckTraitsResponse([], JevUnavailable: true, Reason: failures[0], Requests: answers.Count)
            : new CheckTraitsResponse([.. findings.OrderByDescending(finding => finding.Probability)], Requests: answers.Count);
    }

    private IEnumerable<TraitFinding> Assess(TraitCheckMode mode, double minimum, TraitTask task, BatchDecided decided, string root)
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
                    ["path"] = task.Source.Path,
                    ["lines"] = new JsonArray(task.Source.StartLine, task.Source.EndLine),
                    ["choice"] = choice.Choice,
                    ["p"] = p,
                    ["reported"] = reported,
                });
            }

            if (reported)
            {
                yield return Finding(trait, task.Source, p, root);
            }
        }
    }

    // The probability of a violation: the model's own, else its confidence when it chose it.
    private static double Violation(ChoiceBatchAnswer choice) =>
        choice.Probabilities.TryGetValue(TraitQuestions.Violation, out var probability)
            ? probability
            : string.Equals(choice.Choice, TraitQuestions.Violation, StringComparison.Ordinal) ? choice.Confidence : 0;

    private static TraitFinding Finding(Trait trait, DiffSource source, double p, string root) => new(
        trait.Name,
        trait.Severity == TraitSeverity.Block ? "block" : "warn",
        source.Path,
        p,
        trait.Requirement.Trim(),
        source.StartLine,
        source.EndLine,
        trait.Guidance?.Trim(),
        trait.File.StartsWith(root, StringComparison.Ordinal) ? Path.GetRelativePath(root, trait.File) : trait.File);
}
