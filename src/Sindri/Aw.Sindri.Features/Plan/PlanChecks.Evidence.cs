using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

internal static partial class PlanChecks
{
    [GeneratedRegex(@"^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SetHash { get; }

    /// <summary>PLN-EXEC-001: the last executability review reports executable for every phase.</summary>
    internal static void PhasesExecutable(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("exec-review.json", Tasks))
        {
            return;
        }

        var reviewed = LineJson.Items(facts.Json("exec-review.json"), "phases").GroupBy(PhaseId, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.First(), StringComparer.Ordinal);
        foreach (var phase in Phases(facts))
        {
            var id = PhaseId(phase);
            if (!reviewed.TryGetValue(id, out var review))
            {
                sink.Fail(id, $"the executability review never covered {id}", "run the review over every phase");
            }
            else if (LineJson.Bool(review, "executable") != true || LineJson.Bool(review, "allowedFilesEnough") == false || LineJson.Bool(review, "signalsCheckable") == false)
            {
                var missing = string.Join("; ", LineJson.Strings(review, "contextMissing"));
                sink.Fail(id, $"{id} is not executable{(missing.Length > 0 ? $": {missing}" : string.Empty)}", "give the phase the context, files and checkable signals the review names");
            }
        }
    }

    /// <summary>PLN-HOLD-003: every phase has a hidden set, by hash, with at least workflow.prove.holdoutMin scenarios and one canary.</summary>
    internal static void HoldoutSetsPlanned(LineFacts facts, LineSink sink)
    {
        if (!sink.Need("holdout.json", Tasks))
        {
            return;
        }

        var sets = LineJson.Items(facts.Json("holdout.json"), "phases").GroupBy(PhaseId, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.First(), StringComparer.Ordinal);
        var min = HoldoutMin(facts);
        foreach (var phase in Phases(facts))
        {
            var id = PhaseId(phase);
            if (!sets.TryGetValue(id, out var set))
            {
                sink.Fail(id, $"{id} has no hidden-scenario set", "plan a set for every phase");
                continue;
            }

            var canaries = LineJson.Number(set, "canaries") ?? 0;
            var scenarios = (LineJson.Number(set, "count") ?? 0) - canaries;
            if (!SetHash.IsMatch(LineJson.Str(set, "setHash") ?? string.Empty))
            {
                sink.Fail(id, $"{id}'s set has no sha256 hash", "record the set by its hash");
            }

            if (canaries < 1 || scenarios < min)
            {
                sink.Fail(id, $"{id} has {scenarios.ToString(CultureInfo.InvariantCulture)} hidden scenarios and {canaries.ToString(CultureInfo.InvariantCulture)} canaries; it needs {min.ToString(CultureInfo.InvariantCulture)} and 1", "add hidden scenarios and a canary to the set");
            }
        }
    }
}
