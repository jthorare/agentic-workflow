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
    // The excerpt's hash in an assessment line: enough to group repeats, short enough for a 1 GB log.
    private const int HashLength = 16;

    internal CheckTraitsResponse Read(CheckTraitsRequest request, double minimum, TraitPlan plan, IReadOnlyList<ProviderResult> answers)
    {
        var mode = request.Mode.ToString().ToLowerInvariant();
        List<TraitFinding> findings = [];
        List<string> failures = [];
        var skipped = new Dictionary<string, string>(plan.Skipped, StringComparer.Ordinal);
        var reading = new Reading(request.Mode, minimum, plan);
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

            findings.AddRange(Assess(reading, plan.Tasks[i], decided.Answers, cached: false));
        }

        findings.AddRange(plan.Cached.SelectMany(verdict => Assess(reading, verdict.Task, Single(verdict), cached: true)));

        // Jev is unavailable only when nothing was judged: a cached verdict counts as judged.
        var unavailable = plan.Cached.Count == 0 && failures.Count > 0 && failures.Count == answers.Count - left;
        return Response(unavailable ? [] : findings, unavailable ? failures[0] : null, answers.Count, skipped);
    }

    private static CheckTraitsResponse Response(List<TraitFinding> findings, string? unavailable, int requests, Dictionary<string, string> skipped) =>
        new([.. findings.OrderByDescending(finding => finding.Probability)], JevUnavailable: unavailable is not null, Reason: unavailable, Requests: requests)
        {
            Skipped = skipped.Count == 0 ? CheckTraitsResponse.NoSkips : skipped,
        };

    private static Dictionary<string, BatchAnswer> Single(CachedVerdict verdict) => new(StringComparer.Ordinal) { [verdict.Trait.Name] = verdict.Answer };

    private IEnumerable<TraitFinding> Assess(Reading reading, TraitTask task, IReadOnlyDictionary<string, BatchAnswer> answers, bool cached)
    {
        var (mode, minimum, plan) = reading;
        foreach (var trait in task.Batch)
        {
            if (!answers.TryGetValue(trait.Name, out var raw) || raw is not ChoiceBatchAnswer choice)
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
                    ["hash"] = task.Hash.Length > HashLength ? task.Hash[..HashLength] : task.Hash,
                    ["choice"] = choice.Choice,
                    ["p"] = p,
                    ["reported"] = reported,
                    ["cached"] = cached,
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

    // What every assessment of one check shares.
    private sealed record Reading(TraitCheckMode Mode, double Minimum, TraitPlan Plan);
}
