using Aw.Meta.Core.Config;
using Aw.Meta.Core.Github;

namespace Aw.Meta.Core.Gate;

/// <summary>Turns what GitHub says about a pull request into the steps of a CI gate record. A step's Log is the reason, not a file.</summary>
public static class CiGate
{
    /// <summary>The step name of the review decision.</summary>
    public const string ReviewStep = "review";

    /// <summary>The step name of the merge state.</summary>
    public const string MergeStateStep = "merge-state";

    /// <summary>One step per required check, then the review decision and the merge state.</summary>
    /// <param name="pull">The pull request's review decision and merge state.</param>
    /// <param name="checks">Its required checks.</param>
    /// <returns>The steps; a step passes only for bucket "pass", review APPROVED and merge state CLEAN.</returns>
    public static IReadOnlyList<GateStep> Steps(PrCiState pull, IReadOnlyList<RequiredCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(pull);
        ArgumentNullException.ThrowIfNull(checks);
        return
        [
            .. checks.Select(check => new GateStep(check.Name, Verdict(string.Equals(check.Bucket, "pass", StringComparison.OrdinalIgnoreCase)), Reason(check))),
            new GateStep(ReviewStep, Verdict(pull.ReviewDecision == "APPROVED"), pull.ReviewDecision.Length == 0 ? "no review decision" : pull.ReviewDecision),
            new GateStep(MergeStateStep, Verdict(pull.MergeState == "CLEAN"), pull.MergeState.Length == 0 ? "unknown" : pull.MergeState),
        ];
    }

    /// <summary>The verdict of a set of steps.</summary>
    /// <param name="steps">The steps.</param>
    /// <returns>Pass when every step passed, else Fail.</returns>
    public static GateVerdict Overall(IReadOnlyList<GateStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return steps.All(step => step.Verdict == GateVerdict.Pass) ? GateVerdict.Pass : GateVerdict.Fail;
    }

    /// <summary>Whether a gate record is a CI gate record: its one scope is <see cref="GateConfig.CiScope"/>.</summary>
    /// <param name="gate">The record.</param>
    /// <returns>True for a record gate-run wrote in ci mode.</returns>
    public static bool IsCiRecord(GateRecord gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        return gate.Scopes.Count == 1 && string.Equals(gate.Scopes[0], GateConfig.CiScope, StringComparison.Ordinal);
    }

    private static GateVerdict Verdict(bool ok) => ok ? GateVerdict.Pass : GateVerdict.Fail;

    private static string Reason(RequiredCheck check) =>
        string.Equals(check.Bucket, "pass", StringComparison.OrdinalIgnoreCase) || string.Equals(check.State, check.Bucket, StringComparison.OrdinalIgnoreCase)
            ? check.State
            : $"{check.State} ({check.Bucket})";
}
