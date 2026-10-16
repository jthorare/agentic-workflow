using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Worktrees;

/// <summary>
/// Removes the worktrees the rules allow, one at a time with "git worktree remove" (never forced, so git itself refuses an unclean one),
/// then prunes. Each removal is judged again just before it. Branches are never deleted. A sweep that cannot judge anything removes nothing.
/// </summary>
/// <param name="inspector">Judges the worktrees.</param>
/// <param name="repo">The git calls.</param>
/// <param name="log">The running log.</param>
/// <param name="values">Keeps the last sweep's line.</param>
/// <param name="clock">Dates the log line.</param>
public sealed class WorktreeSweeper(WorktreeInspector inspector, RepoCommands repo, IRunningLog log, IKeyValueStore values, TimeProvider clock)
{
    private const int ListedPaths = 3;

    /// <summary>Sweeps a scope.</summary>
    /// <param name="team">The team config.</param>
    /// <param name="scope">Which worktrees to consider.</param>
    /// <param name="mode">Remove, or only report.</param>
    /// <param name="trigger">Names who asked (sweep, merge-run, watch); goes into the log line.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was removed and kept, or an error when nothing could be judged.</returns>
    public async ValueTask<Result<SweepOutcome>> Sweep(TeamConfig team, WorktreeScope scope, SweepMode mode, string trigger, CancellationToken cancellationToken)
    {
        var done = await Run(new Job(team, scope, mode, trigger, Record: mode == SweepMode.Remove), cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : done.Value.Outcome;
    }

    /// <summary>The sweep a trigger runs for pull requests that ended: removes (or, with the config's Enabled false, only reports), logs a line and keeps it as the last sweep.</summary>
    /// <param name="team">The team config.</param>
    /// <param name="prs">The pull requests that merged or closed.</param>
    /// <param name="trigger">merge-run or watch.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The one summary line (empty when no worktree belongs to those pull requests), or an error.</returns>
    public async ValueTask<Result<string>> Triggered(TeamConfig team, IReadOnlyCollection<int> prs, string trigger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(prs);
        var mode = team.Worktrees.Enabled ? SweepMode.Remove : SweepMode.DryRun;
        var done = await Run(new Job(team, WorktreeScope.ForPrs(prs), mode, trigger, Record: true), cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : done.Value.Line.Length == 0 ? string.Empty : "WORKTREES" + done.Value.Line["worktrees".Length..];
    }

    private sealed record Job(TeamConfig Team, WorktreeScope Scope, SweepMode Mode, string Trigger, bool Record);

    private async ValueTask<Result<(SweepOutcome Outcome, string Line)>> Run(Job job, CancellationToken cancellationToken)
    {
        var (team, scope, mode, trigger, record) = job;
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(scope);
        var started = clock.GetUtcNow();
        var inspected = await inspector.Inspect(team, scope, fetch: true, onlyPath: null, cancellationToken).ConfigureAwait(false);
        if (inspected.IsError)
        {
            return inspected.Error;
        }

        var outcome = await Apply(team, scope, mode, inspected.Value, cancellationToken).ConfigureAwait(false);
        if (mode == SweepMode.Remove)
        {
            // The marker moves only after the removals, which judged again against the previous one; the next sweep keeps whatever changed after this one began.
            _ = await values.Set(KvKeys.WorktreesSweepStart, started.ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        }

        var line = outcome.Removed.Count + outcome.Kept.Count == 0 ? string.Empty : Summary(trigger, scope, outcome);
        if (record && line.Length > 0)
        {
            _ = await log.Append(LogLine.Event(LogKind.Note, clock.GetUtcNow(), line), cancellationToken).ConfigureAwait(false);
            _ = await values.Set(KvKeys.WorktreesLast, line, cancellationToken).ConfigureAwait(false);
        }

        return (outcome, line);
    }

    private async ValueTask<SweepOutcome> Apply(TeamConfig team, WorktreeScope scope, SweepMode mode, IReadOnlyList<WorktreeReport> reports, CancellationToken cancellationToken)
    {
        List<WorktreeReport> removed = [];
        List<WorktreeReport> kept = [];
        foreach (var report in reports)
        {
            var refused = report.Decision != WorktreeDecision.Removable ? report
                : mode == SweepMode.DryRun ? null
                : await Remove(team, scope, report, cancellationToken).ConfigureAwait(false);
            (refused is null ? removed : kept).Add(refused ?? report);
        }

        if (mode == SweepMode.Remove && removed.Count > 0)
        {
            _ = await repo.Git(team.Repo, "worktree", "prune").ConfigureAwait(false);
        }

        return new SweepOutcome(mode == SweepMode.DryRun, removed, kept, kept.Count(static r => r.Reasons.Any(static x => x.StartsWith(RefusedPrefix, StringComparison.Ordinal))));
    }

    private const string RefusedPrefix = "git refused to remove it: ";

    // Null when removed; else the report, now kept, with why. The verdict is taken again first: a process may have started since the inspection.
    private async ValueTask<WorktreeReport?> Remove(TeamConfig team, WorktreeScope scope, WorktreeReport report, CancellationToken cancellationToken)
    {
        var again = await inspector.Inspect(team, scope, fetch: false, report.Path, cancellationToken).ConfigureAwait(false);
        if (again.IsError || again.Value.Count != 1 || again.Value[0].Decision != WorktreeDecision.Removable)
        {
            return report with { Decision = WorktreeDecision.Kept, Reasons = again.IsError ? ["could not check it again before removing: " + again.Error.Message] : again.Value.Count == 1 ? again.Value[0].Reasons : ["it was gone before it could be removed"] };
        }

        var removed = await repo.Git(team.Repo, "worktree", "remove", report.Path).ConfigureAwait(false);
        return removed.IsError ? report with { Decision = WorktreeDecision.Kept, Reasons = [RefusedPrefix + removed.Error.Message.ReplaceLineEndings(" ")] } : null;
    }

    // "worktrees merge-run #7: removed 2 (/a, /b); kept 1 (/c: reason; reason)".
    private static string Summary(string trigger, WorktreeScope scope, SweepOutcome outcome)
    {
        var prs = scope.Prs is { Count: > 0 } set ? " " + string.Join(',', set.Order().Select(static n => "#" + n.ToString(CultureInfo.InvariantCulture))) : string.Empty;
        var verb = outcome.DryRun ? "would remove" : "removed";
        var removed = $"{verb} {outcome.Removed.Count.ToString(CultureInfo.InvariantCulture)}{Listed(outcome.Removed.Select(static r => r.Path))}";
        var kept = $"kept {outcome.Kept.Count.ToString(CultureInfo.InvariantCulture)}{Listed(outcome.Kept.Select(static r => r.Path + ": " + string.Join("; ", r.Reasons)))}";
        return $"worktrees {trigger}{prs}: {removed}; {kept}";
    }

    private static string Listed(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count == 0 ? string.Empty : " (" + string.Join(", ", list.Take(ListedPaths)) + (list.Count > ListedPaths ? ", ..." : string.Empty) + ")";
    }
}
