using System.Globalization;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Core.Merge;

/// <summary>The merge preconditions.</summary>
public static class MergeGate
{
    private const string NoGate = "no gate recorded for this pull request";

    /// <summary>Evaluates every precondition: gate, gate-result, head, main, order, draft. With no gate recorded and no reviewer
    /// lane, only the gate check. With a reviewer lane (<see cref="MergeFacts.Review"/>), the review path (review, ready, counts, order, draft) is
    /// enough on its own, with no check against main; a red gate recorded at the current head still refuses.</summary>
    /// <param name="facts">What is known about the pull request and main.</param>
    /// <returns>One check per precondition, in that order.</returns>
    public static IReadOnlyList<MergeCheck> Evaluate(MergeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.CiGate)
        {
            return CiChecks(facts);
        }

        var gate = facts.Gate;
        var review = facts.Review is null ? null : ReviewChecks(facts, facts.Review);
        if (gate is null || review is null || !review.All(check => check.Ok))
        {
            return gate is null ? NoGateChecks(facts, review) : GateChecks(facts, gate);
        }

        // A red gate recorded at this head still refuses; a gate at another head says nothing about it.
        var red = gate.Head == facts.CurrentHead ? GateResult(gate, facts.FailingTests, facts.FailureCounts) : null;
        return red is { Ok: false } ? [.. review, red] : review;
    }

    // A CI gate: the newest record must be the read of the pull request's required checks, passing, at the head now. Nothing is
    // checked against main, no scope is planned and no test log is read. The record is from gate time, so the merge state and the
    // review decision are read from GitHub again at merge time and must be CLEAN and APPROVED now; a failed read refuses. The
    // review path adds the reviewer's OK, the ready line and the counts when the team has a reviewer lane.
    private static MergeCheck[] CiChecks(MergeFacts facts)
    {
        var rest = facts.Review is null ? [Order(facts), Draft(facts)] : ReviewChecks(facts, facts.Review);
        if (facts.Gate is not { } gate)
        {
            return [new MergeCheck("gate", Ok: false, NoGate + "; run aw-meta gate-run " + facts.Pr.ToString(CultureInfo.InvariantCulture) + " (it reads the required checks)"), .. rest];
        }

        if (!CiGate.IsCiRecord(gate))
        {
            return [new MergeCheck("gate", Ok: false, $"the newest gate run {gate.Run.ToString(CultureInfo.InvariantCulture)} ran {string.Join(", ", gate.Scopes)} locally, not CI; run aw-meta gate-run {facts.Pr.ToString(CultureInfo.InvariantCulture)} (gate.mode is ci)"), .. rest];
        }

        var failed = gate.Steps.Where(step => step.Verdict != GateVerdict.Pass).Select(step => $"{step.Name} ({step.Log})").ToList();
        return
        [
            new MergeCheck("gate", Ok: true, $"CI gate run {gate.Run.ToString(CultureInfo.InvariantCulture)} at {gate.Head}"),
            gate.Verdict == GateVerdict.Pass && failed.Count == 0
                ? new MergeCheck("gate-result", Ok: true, "PASS")
                : new MergeCheck("gate-result", Ok: false, $"{gate.Verdict}: {(failed.Count == 0 ? "no failing step recorded" : string.Join("; ", failed))}"),
            gate.Head == facts.CurrentHead
                ? new MergeCheck("head", Ok: true, "head unchanged since the CI gate")
                : new MergeCheck("head", Ok: false, $"head moved from {gate.Head} to {facts.CurrentHead} since the CI gate"),
            .. Live(facts),
            .. rest,
        ];
    }

    private static MergeCheck[] Live(MergeFacts facts)
    {
        if (facts.LiveError is not null || facts.LiveMergeState is null || facts.LiveReviewDecision is null)
        {
            var why = $"could not read the live state from GitHub: {facts.LiveError ?? "no answer"}";
            return [new MergeCheck("merge-state", Ok: false, why), new MergeCheck("review-decision", Ok: false, why)];
        }

        var state = facts.LiveMergeState;
        var decision = facts.LiveReviewDecision;
        return
        [
            string.Equals(state, "CLEAN", StringComparison.Ordinal)
                ? new MergeCheck("merge-state", Ok: true, "CLEAN now")
                : new MergeCheck("merge-state", Ok: false, $"GitHub's merge state is {(state.Length == 0 ? "unknown" : state)} now, not CLEAN"),
            string.Equals(decision, "APPROVED", StringComparison.Ordinal)
                ? new MergeCheck("review-decision", Ok: true, "APPROVED now")
                : new MergeCheck("review-decision", Ok: false, $"GitHub's review decision is {(decision.Length == 0 ? "none" : decision)} now, not APPROVED"),
        ];
    }

    private static MergeCheck Draft(MergeFacts facts) => new("draft", !facts.IsDraft, facts.IsDraft ? "the pull request is a draft" : "ready for review");

    // With a reviewer lane no gate is required (merge direct, #2894): the review checks alone decide, and a failing one says
    // what is missing, so no "gate" check is added that would send meta to gate-run.
    private static MergeCheck[] NoGateChecks(MergeFacts facts, MergeCheck[]? review) =>
        review ?? [new MergeCheck("gate", Ok: false, NoGate + "; run aw-meta gate-run " + facts.Pr.ToString(CultureInfo.InvariantCulture))];

    private static MergeCheck[] GateChecks(MergeFacts facts, GateRecord gate)
    {
        return
        [
            new MergeCheck("gate", Ok: true, "gate run " + gate.Run.ToString(CultureInfo.InvariantCulture) + " at " + gate.Head),
            GateResult(gate, facts.FailingTests, facts.FailureCounts),
            Scopes(gate, facts.PlannedScopes, facts.ScopeRuns),
            Head(facts),
            Main(gate, facts),
            Order(facts),
            new MergeCheck("draft", !facts.IsDraft, facts.IsDraft ? "the pull request is a draft" : "ready for review"),
        ];
    }

    // The review path: the reviewer's OK at the head, the lane's ready at the head, its counts, order and draft. Nothing is checked against main.
    private static MergeCheck[] ReviewChecks(MergeFacts facts, ReviewFacts review)
    {
        var pr = facts.Pr.ToString(CultureInfo.InvariantCulture);
        var ok = ReviewCheck(review, pr, facts.CurrentHead);
        var ready = review.ReadySha is null
            ? new MergeCheck("ready", Ok: false, $"no ready message for #{pr} naming a sha")
            : ReviewProof.IsHead(review.ReadySha, facts.CurrentHead)
                ? new MergeCheck("ready", Ok: true, $"ready at {review.ReadySha}")
                : new MergeCheck("ready", Ok: false, $"the newest ready is at {review.ReadySha}, the head is {facts.CurrentHead}");
        var failed = review.Counts.Where(c => c.Failed > 0).ToList();
        var counts = review.Counts.Count == 0
            ? new MergeCheck("counts", Ok: false, "the ready message has no \"N passed, M skipped, K failed\" line")
            : failed.Count > 0
                ? new MergeCheck("counts", Ok: false, $"the ready message counts {failed.Sum(c => c.Failed).ToString(CultureInfo.InvariantCulture)} failed")
                : new MergeCheck("counts", Ok: true, $"{review.Counts.Count.ToString(CultureInfo.InvariantCulture)} count line(s), 0 failed");
        return [ok, ready, counts, Order(facts), new MergeCheck("draft", !facts.IsDraft, facts.IsDraft ? "the pull request is a draft" : "ready for review")];
    }

    // The reviewer's standing OK must name the head; a qa line under it ("qa not yet", "qa passed <sha>") must say passed at the head.
    private static MergeCheck ReviewCheck(ReviewFacts review, string pr, string head)
    {
        if (review.OkSha is null)
        {
            return new MergeCheck("review", Ok: false, $"no standing \"OK #{pr} at <sha>\" from {review.Reviewer}");
        }

        if (!ReviewProof.IsHead(review.OkSha, head))
        {
            return new MergeCheck("review", Ok: false, $"{review.Reviewer}'s newest OK is at {review.OkSha}, the head is {head}");
        }

        if (review.QaNotYet)
        {
            return new MergeCheck("review", Ok: false, $"{review.Reviewer} said OK at {review.OkSha} but qa not yet");
        }

        return review.QaSha is null || ReviewProof.IsHead(review.QaSha, head)
            ? new MergeCheck("review", Ok: true, $"{review.Reviewer} said OK at {review.OkSha}" + (review.QaSha is null ? string.Empty : $", qa passed at {review.QaSha}"))
            : new MergeCheck("review", Ok: false, $"qa passed at {review.QaSha}, the head is {head}");
    }

    private static MergeCheck GateResult(GateRecord gate, IReadOnlyDictionary<string, IReadOnlyList<string>> failing, IReadOnlyDictionary<string, (int Summarized, int Named)> counts)
    {
        if (gate.Verdict == GateVerdict.Pass)
        {
            return new MergeCheck("gate-result", Ok: true, "PASS");
        }

        var red = gate.Steps.Where(s => s.Verdict != GateVerdict.Pass).ToList();
        if (gate.Verdict != GateVerdict.Fail || red.Count == 0)
        {
            return new MergeCheck("gate-result", Ok: false, $"{gate.Verdict}: no failing step recorded");
        }

        var unexplained = red.Select(step => Unexplained(step, failing, counts)).Where(text => text is not null).ToList();
        return unexplained.Count == 0
            ? new MergeCheck("gate-result", Ok: true, "every failing test of every red step is a classified flake")
            : new MergeCheck("gate-result", Ok: false, $"{gate.Verdict}: {string.Join("; ", unexplained)}");
    }

    // A red step is accepted only when its log names its failing tests and every one of them was rerun and classified a flake.
    private static string? Unexplained(GateStep step, IReadOnlyDictionary<string, IReadOnlyList<string>> failing, IReadOnlyDictionary<string, (int Summarized, int Named)> counts)
    {
        if (!failing.TryGetValue(step.Name, out var tests) || tests.Count == 0)
        {
            return $"{step.Name}: could not list its failing tests from the log, so no flake classification can vouch for it";
        }

        if (counts.TryGetValue(step.Name, out var count) && count.Summarized != count.Named)
        {
            return $"{step.Name}: the log's summary counts {count.Summarized.ToString(CultureInfo.InvariantCulture)} failed but only {count.Named.ToString(CultureInfo.InvariantCulture)} name a test (a crash or a timeout leaves no name), so no flake classification can vouch for it";
        }

        var open = tests.Where(test => !step.Reruns.Any(r => r.Verdict == FlakeVerdict.Flake && TestNames.Matches(r.Test, test))).ToList();
        return open.Count == 0 ? null : $"not a flake in {step.Name}: {string.Join(", ", open)}";
    }

    // A gate run with fewer scopes than the diff needs (gate-run <pr> repo on a code change) vouches only for what it ran.
    private static MergeCheck Scopes(GateRecord gate, IReadOnlyList<string>? planned, IReadOnlyDictionary<string, int>? runs)
    {
        var ran = string.Join(", ", gate.Scopes.Select(scope => runs is not null && runs.TryGetValue(scope, out var run) ? $"{scope} (run {run.ToString(CultureInfo.InvariantCulture)})" : scope));
        if (planned is null)
        {
            return new MergeCheck("scopes", Ok: false, $"could not work out the scopes the diff needs; the gate ran {ran}");
        }

        var missing = planned.Where(scope => !gate.Scopes.Contains(scope, StringComparer.Ordinal)).ToList();
        return missing.Count == 0
            ? new MergeCheck("scopes", Ok: true, $"ran every scope the diff needs ({ran})")
            : new MergeCheck("scopes", Ok: false, $"the diff needs {string.Join(", ", missing)}; the gate ran {ran}");
    }

    private static MergeCheck Head(MergeFacts facts)
    {
        if (facts.Gate is not { } gate)
        {
            return new MergeCheck("head", Ok: false, NoGate);
        }

        if (gate.Head == facts.CurrentHead)
        {
            return new MergeCheck("head", Ok: true, "head unchanged since the gate");
        }

        var docsOnly = facts.DeltaPaths is { } delta
            && delta.All(path => facts.DocsOnly.Any(glob => GlobMatcher.IsMatch(glob, path)));
        return docsOnly && facts.RepoGatePassedOnHead
            ? new MergeCheck("head", Ok: true, "head moved by a docs-only change and the repo gate passed on it")
            : new MergeCheck("head", Ok: false, $"head moved from {gate.Head} to {facts.CurrentHead} since the gate");
    }

    // The fast path (same main) holds for an unstacked gate only: a stacked gate tested a main plus other pull requests' heads,
    // so it is valid only while merging its head onto main today still gives the tested tree. The same test lets a gate
    // whose main moved stand. A gate recorded before the tree was kept (Tree null) keeps the old rule.
    private static MergeCheck Main(GateRecord gate, MergeFacts facts)
    {
        if (gate.Stack.Count == 0 && gate.Main == facts.CurrentMain)
        {
            return new MergeCheck("main", Ok: true, $"gate ran on main {gate.Main}");
        }

        if (gate.Tree is not null && facts.MergedTree == gate.Tree)
        {
            return new MergeCheck("main", Ok: true, gate.Stack.Count == 0
                ? $"main moved from {gate.Main} to {facts.CurrentMain}, and the gated head merges onto it to the tested tree"
                : $"the gated head merges onto main {facts.CurrentMain} to the tested tree (the gate stacked {string.Join(", ", gate.Stack.Select(s => "#" + s.Pr.ToString(CultureInfo.InvariantCulture)))})");
        }

        // A merge queue re-tests the merged tree before it lands, so a main that moved past the gate is the queue's to settle.
        if (facts.BaseHasMergeQueue)
        {
            return new MergeCheck("main", Ok: true, gate.Main == facts.CurrentMain
                ? $"the gated head does not merge to the tested tree on main {facts.CurrentMain}; GitHub's merge queue re-tests on the merged tree"
                : $"main moved from {gate.Main} to {facts.CurrentMain}; GitHub's merge queue re-tests on the merged tree");
        }

        if (gate.Tree is null)
        {
            return new MergeCheck("main", Ok: false, $"gate ran on main {gate.Main}, main is {facts.CurrentMain}");
        }

        return new MergeCheck("main", Ok: false, gate.Stack.Count == 0
            ? $"gate ran on main {gate.Main}, main is {facts.CurrentMain}, and the gated head no longer merges onto it to the tested tree"
            : $"the gate stacked {string.Join(", ", gate.Stack.Select(s => "#" + s.Pr.ToString(CultureInfo.InvariantCulture)))}; merging the gated head onto main {facts.CurrentMain} does not give the tested tree");
    }

    private static MergeCheck Order(MergeFacts facts)
    {
        var waiting = facts.MergeOrder
            .Where(rule => rule.After == facts.Pr && facts.OpenPrs.Contains(rule.Before))
            .Select(rule => "#" + rule.Before.ToString(CultureInfo.InvariantCulture))
            .ToList();
        return waiting.Count == 0
            ? new MergeCheck("order", Ok: true, "no earlier pull request is waiting")
            : new MergeCheck("order", Ok: false, "must merge after " + string.Join(", ", waiting));
    }
}
