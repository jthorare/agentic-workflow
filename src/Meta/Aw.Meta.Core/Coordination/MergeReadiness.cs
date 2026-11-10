using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Gathers the facts the merge gate decides on (the recorded gates, GitHub's view of the pull request, origin/main, the earlier
/// pull requests it must wait for) and runs MergeGate over them. A pull request that was never gated is refused without asking GitHub.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="gates">The recorded gate runs.</param>
/// <param name="repo">The git and gh calls.</param>
/// <param name="logs">Reads the red steps' logs for their failing tests.</param>
/// <param name="messages">The lanes' messages, where a reviewer's OK and a lane's ready notice are read.</param>
public sealed class MergeReadiness(IMetaConfig config, IGateStore gates, RepoCommands repo, ILogFiles logs, IMessageStore messages)
{
    // The reviewer path (#2610) reads the lanes' messages through this; nothing does yet.
#pragma warning disable CA1822, MA0041 // a stub: the reviewer path will read the instance's messages
    internal IMessageStore Messages => messages;
#pragma warning restore CA1822, MA0041

    private const string Fields = "headRefOid,isDraft,title,state";
    private const int LogCharacters = 32 * 1024 * 1024;

    /// <summary>Checks a pull request.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The checks, or an error from the store, git or gh.</returns>
    public async ValueTask<Result<MergeAssessment>> Assess(int pr, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        var history = await gates.History(pr, cancellationToken).ConfigureAwait(false);
        if (team.IsError || history.IsError)
        {
            return team.IsError ? team.Error : history.Error;
        }

        if (history.Value.Count == 0)
        {
            return NeverGated(team.Value, pr);
        }

        var current = await Current(team.Value.Repo, pr).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var (view, main) = current.Value;
        var (gate, repoPassed, scopeRuns) = Choose(history.Value, view.Head, team.Value.Gate);
        var (names, counts) = await FailingTests(gate).ConfigureAwait(false);
        var facts = new MergeFacts
        {
            Pr = pr,
            Gate = gate,
            CurrentHead = view.Head,
            CurrentMain = main,
            IsDraft = view.IsDraft,
            DeltaPaths = await Delta(team.Value.Repo, gate, view.Head).ConfigureAwait(false),
            RepoGatePassedOnHead = repoPassed,
            OpenPrs = await OpenBefore(team.Value, pr).ConfigureAwait(false),
            MergeOrder = team.Value.MergeOrder,
            DocsOnly = team.Value.Gate.DocsOnly,
            PlannedScopes = await Planned(team.Value, gate).ConfigureAwait(false),
            ScopeRuns = scopeRuns,
            FailingTests = names,
            FailureCounts = counts,
        };
        return new MergeAssessment(team.Value, [.. MergeGate.Evaluate(facts with { MergedTree = await MergedTree(team.Value.Repo, gate, main).ConfigureAwait(false) }), State(view)], view);
    }

    private static MergeAssessment NeverGated(TeamConfig team, int pr) =>
        new(team, MergeGate.Evaluate(new MergeFacts { Pr = pr, CurrentHead = string.Empty, CurrentMain = string.Empty }), Pr: null);

    private async ValueTask<Result<(PrDetails View, string Main)>> Current(string repoDirectory, int pr)
    {
        var fetched = await repo.Fetch(repoDirectory, pr).ConfigureAwait(false);
        var view = fetched.IsError ? Results.Failure<PrDetails>(fetched.Error) : await repo.View(repoDirectory, pr, Fields).ConfigureAwait(false);
        var main = view.IsError ? Results.Failure<string>(view.Error) : await repo.Sha(repoDirectory, "origin/main").ConfigureAwait(false);
        return main.IsError ? main.Error : (view.Value, main.Value);
    }

    // The gate the merge rests on. When the latest run was only the always scopes (a re-check after a docs commit), it vouches
    // for the new head but not for the code: the newest run that went beyond them is the one the head is measured from.
    private static (GateRecord Gate, bool RepoPassedOnHead, IReadOnlyDictionary<string, int> ScopeRuns) Choose(IReadOnlyList<GateRecord> history, string head, GateConfig config)
    {
        var (chosen, repoPassed) = ChooseRun(history, head, config.Always);
        var (combined, scopeRuns) = Combine(history, chosen, config);
        return (combined, repoPassed, scopeRuns);
    }

    private static (GateRecord Gate, bool RepoPassedOnHead) ChooseRun(IReadOnlyList<GateRecord> history, string head, IReadOnlyList<string> always)
    {
        var latest = history[0];
        var beyond = history.FirstOrDefault(g => g.Scopes.Any(scope => !always.Contains(scope, StringComparer.Ordinal)));
        return beyond is not null && beyond.Run != latest.Run && latest.Scopes.All(scope => always.Contains(scope, StringComparer.Ordinal))
            ? (beyond, latest.Head == head && latest.Verdict == GateVerdict.Pass)
            : (latest, false);
    }

    // Every run at the chosen run's head and main counts: a scope is judged by the newest of those runs that ran it, so a
    // scoped re-gate (gate-run <pr> bash-scripts) completes an earlier run instead of replacing it. Runs at another head
    // or main never count. Steps are matched to scopes by the team config; a step no scope names stays with the chosen run.
    private static (GateRecord Gate, IReadOnlyDictionary<string, int> ScopeRuns) Combine(IReadOnlyList<GateRecord> history, GateRecord chosen, GateConfig config)
    {
        var same = history.Where(g => g.Head == chosen.Head && g.Main == chosen.Main && g.Tree == chosen.Tree).ToList();
        var known = config.Scopes.Values.SelectMany(scope => scope.Steps.Select(step => step.Name)).ToHashSet(StringComparer.Ordinal);
        var scopeRuns = new Dictionary<string, int>(StringComparer.Ordinal);
        var steps = chosen.Steps.Where(step => !known.Contains(step.Name)).ToList();
        var used = new List<GateRecord>();
        foreach (var scope in same.SelectMany(g => g.Scopes).Distinct(StringComparer.Ordinal))
        {
            var run = same.First(g => g.Scopes.Contains(scope, StringComparer.Ordinal));   // newest first
            scopeRuns[scope] = run.Run;
            used.Add(run);
            var names = config.Scopes.TryGetValue(scope, out var scopeConfig) ? scopeConfig.Steps.Select(step => step.Name).ToHashSet(StringComparer.Ordinal) : [];
            steps.AddRange(run.Steps.Where(step => names.Contains(step.Name) && !steps.Exists(have => have.Name == step.Name)));
        }

        var verdict = steps.Exists(step => step.Verdict != GateVerdict.Pass)
            ? GateVerdict.Fail
            : used.Where(run => run.Verdict != GateVerdict.Pass && run.Steps.All(step => step.Verdict == GateVerdict.Pass)).Select(run => run.Verdict).FirstOrDefault(GateVerdict.Pass);
        return (chosen with { Scopes = [.. scopeRuns.Keys], Steps = steps, Verdict = verdict }, scopeRuns);
    }

    // Only a gate that recorded its tree and is not simply current (stacked, or main moved) needs the merge-tree answer.
    private async ValueTask<string?> MergedTree(string repoDirectory, GateRecord gate, string main) =>
        gate.Tree is null || (gate.Stack.Count == 0 && gate.Main == main) ? null : await repo.MergeTree(repoDirectory, main, gate.Head).ConfigureAwait(false);

    private async ValueTask<IReadOnlyList<string>?> Delta(string repoDirectory, GateRecord gate, string head)
    {
        if (gate.Head == head)
        {
            return null;
        }

        var changed = await repo.Changed(repoDirectory, $"{gate.Head}..{head}").ConfigureAwait(false);
        return changed.IsSuccess ? changed.Value : null;
    }

    // The scopes the gated code needs, from the same planner gate-run uses, over the diff the gate merged.
    private async ValueTask<IReadOnlyList<string>?> Planned(TeamConfig team, GateRecord gate)
    {
        var changed = await repo.Changed(team.Repo, $"{gate.Main}...{gate.Head}").ConfigureAwait(false);
        return changed.IsSuccess ? ScopePlanner.Plan(changed.Value, team.Gate) : null;
    }

    // The failing tests each red step's log names. A log that can't be read, or is longer than what is read (its first failures
    // may be cut off), leaves the step out: it can't be vouched for.
    private async ValueTask<(IReadOnlyDictionary<string, IReadOnlyList<string>> Names, IReadOnlyDictionary<string, (int Summarized, int Named)> Counts)> FailingTests(GateRecord gate)
    {
        var names = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var counts = new Dictionary<string, (int Summarized, int Named)>(StringComparer.Ordinal);
        foreach (var step in gate.Steps.Where(s => s.Verdict != GateVerdict.Pass))
        {
            var log = await logs.Tail(step.Log, LogCharacters, CancellationToken.None).ConfigureAwait(false);
            if (log.IsSuccess && log.Value.Length < LogCharacters)
            {
                names[step.Name] = TestNames.Failing(log.Value);
                if (TestNames.SummarizedFailures(log.Value) is { } summarized)
                {
                    counts[step.Name] = (summarized, TestNames.FailedLines(log.Value));
                }
            }
        }

        return (names, counts);
    }

    private async ValueTask<IReadOnlyList<int>> OpenBefore(TeamConfig team, int pr)
    {
        var open = new List<int>();
        foreach (var rule in team.MergeOrder.Where(rule => rule.After == pr))
        {
            var earlier = await repo.View(team.Repo, rule.Before, "state").ConfigureAwait(false);
            if (earlier.IsError || earlier.Value.State == PrState.Open)
            {
                open.Add(rule.Before);   // when it can't be read, assume it is still open: the safe side
            }
        }

        return open;
    }

    private static MergeCheck State(PrDetails view) =>
        new("state", view.State == PrState.Open, view.State == PrState.Open ? "open" : view.State.ToString().ToLowerInvariant());
}
