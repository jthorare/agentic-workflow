using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Worktrees;

/// <summary>Gathers the facts about every worktree of the team's repo and judges each by <see cref="WorktreeRules"/>.</summary>
/// <param name="repo">The git and gh calls.</param>
/// <param name="processes">The process scan.</param>
/// <param name="paths">Where the gate trees live; the home directory.</param>
/// <param name="values">Holds when the last sweep started.</param>
/// <param name="files">When a worktree's files last changed.</param>
/// <param name="bugfix">The bugfix runs' records: --discarded, and the trees of runs whose issue a merged pull request closed.</param>
public sealed partial class WorktreeInspector(RepoCommands repo, IProcessLocations processes, MetaPaths paths, IKeyValueStore values, IWorktreeFiles files, IBugfixRecords bugfix)
{
    private const string GateCommitter = "aw-meta@localhost";
    private static readonly string[] _gateCommands = ["gate-run", "gate-rerun", "merge-run"];
    private const string GateHost = "aw-meta";

    /// <summary>Inspects the worktrees in a scope.</summary>
    /// <param name="team">The team config (the repo, the worktree settings).</param>
    /// <param name="scope">Which worktrees to look at.</param>
    /// <param name="fetch">Whether to fetch origin first (a sweep always does; a read-only list may skip it).</param>
    /// <param name="onlyPath">When given, just that worktree (the re-check just before a removal).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One report per worktree, or an error when the repo, GitHub or the process scan can't be read (a sweep then removes nothing).</returns>
    public async ValueTask<Result<IReadOnlyList<WorktreeReport>>> Inspect(TeamConfig team, WorktreeScope scope, bool fetch, string? onlyPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(scope);
        var world = await World(team, fetch, cancellationToken).ConfigureAwait(false);
        if (world.IsError)
        {
            return world.Error;
        }

        var since = await values.Get(KvKeys.WorktreesSweepStart, cancellationToken).ConfigureAwait(false);
        if (since.IsError)
        {
            return since.Error;
        }

        var treeSince = scope.Path is { } treePath ? await values.Get(KvKeys.WorktreesPathSweepStart(WorktreeRoots.Normalize(treePath)), cancellationToken).ConfigureAwait(false) : Results.Success<string?>(value: null);
        if (treeSince.IsError)
        {
            return treeSince.Error;
        }

        var (entries, pulls, scan) = world.Value;
        var repoRoot = await RepoWorktreeRoot.ResolveOrNull(files, team.Repo, paths.Home, cancellationToken).ConfigureAwait(false);
        var inspection = new Inspection(team, paths, repoRoot, pulls, scan, Later(Parse(since.Value), Parse(treeSince.Value)), cancellationToken)
        {
            Scope = scope,
        };
        var reports = new List<WorktreeReport>();
        foreach (var entry in entries)
        {
            if ((onlyPath is null || Same(entry.Path, onlyPath)) && await InScope(inspection, entry, scope).ConfigureAwait(false))
            {
                reports.Add(await Report(inspection, entry).ConfigureAwait(false));
            }
        }

        return reports;
    }

    private static DateTimeOffset? Parse(string? text) => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;

    private static DateTimeOffset? Later(DateTimeOffset? a, DateTimeOffset? b) => a is { } x && b is { } y ? (x > y ? x : y) : a ?? b;

    private async ValueTask<Result<(IReadOnlyList<WorktreeEntry> Entries, List<PrBranch> Pulls, IReadOnlyList<ProcessLocation> Scan)>> World(TeamConfig team, bool fetch, CancellationToken cancellationToken)
    {
        var directory = team.Repo;
        var fetched = fetch ? await repo.Fetch(directory, pr: null).ConfigureAwait(false) : Results.Ok();
        var main = fetched.IsError ? Results.Failure<string>(fetched.Error) : await repo.Sha(directory, "origin/main").ConfigureAwait(false);
        var listed = main.IsError ? Results.Failure<string>(main.Error) : await repo.Git(directory, "worktree", "list", "--porcelain").ConfigureAwait(false);
        var pulls = listed.IsError ? Results.Failure<IReadOnlyList<PrBranch>>(listed.Error) : await repo.Branches(directory).ConfigureAwait(false);
        var scan = pulls.IsError ? Results.Failure<IReadOnlyList<ProcessLocation>>(pulls.Error) : await processes.Locations(cancellationToken).ConfigureAwait(false);
        return scan.IsError
            ? scan.Error
            : (PorcelainWorktrees.Parse(listed.Value), [.. pulls.Value], scan.Value);
    }

    private async ValueTask<bool> InScope(Inspection inspection, WorktreeEntry entry, WorktreeScope scope)
    {
        if (scope.Path is { } path)
        {
            return Same(entry.Path, path);
        }

        if (scope.Prs is not { } prs)
        {
            return true;
        }

        foreach (var number in prs)
        {
            var pull = await PullRequest(inspection, number).ConfigureAwait(false);
            if ((entry.Branch is not null && pull?.HeadRef == entry.Branch) || inspection.GatePr(entry.Path) == number)
            {
                return true;
            }
        }

        return await ClosedByOneOf(inspection, entry, prs).ConfigureAwait(false);
    }

    // A pull request from the list, else one `gh pr view` away (an old one fell off the newest 200); null when GitHub doesn't know it.
    private async ValueTask<PrBranch?> PullRequest(Inspection inspection, int number)
    {
        if (inspection.Pulls.FirstOrDefault(p => p.Number == number) is { } listed)
        {
            return listed;
        }

        var viewed = await repo.View(inspection.Team.Repo, number, "headRefOid,headRefName,state,mergedAt,closedAt").ConfigureAwait(false);
        if (viewed.IsError)
        {
            return null;
        }

        var pull = new PrBranch(number, viewed.Value.State, viewed.Value.HeadRef, viewed.Value.Head, viewed.Value.EndedAt);
        inspection.Pulls.Add(pull);
        return pull;
    }

    private async ValueTask<WorktreeReport> Report(Inspection inspection, WorktreeEntry entry)
    {
        var gatePr = inspection.GatePr(entry.Path);
        var pull = gatePr is { } number
            ? await PullRequest(inspection, number).ConfigureAwait(false)
            : entry.Branch is null ? null : Choose(inspection.Pulls.Where(p => p.HeadRef == entry.Branch));
        var facts = await Facts(inspection, entry, gatePr, pull).ConfigureAwait(false);
        if (facts.IsError)
        {
            return new WorktreeReport(entry.Path, entry.Branch, entry.Head, pull?.Number ?? gatePr, pull?.State, WorktreeDecision.Kept, ["could not inspect it: " + facts.Error.Message.ReplaceLineEndings(" ")]);
        }

        var verdict = WorktreeRules.Judge(facts.Value);
        return new WorktreeReport(entry.Path, entry.Branch, entry.Head, pull?.Number ?? gatePr, pull?.State, verdict.Decision, verdict.Reasons);
    }

    // The open pull request of a branch if there is one, else the newest.
    private static PrBranch? Choose(IEnumerable<PrBranch> candidates) =>
        candidates.OrderByDescending(p => p.State == PrState.Open).ThenByDescending(p => p.Number).FirstOrDefault();

    private async ValueTask<Result<WorktreeFacts>> Facts(Inspection inspection, WorktreeEntry entry, int? gatePr, PrBranch? pull)
    {
        var git = await Gather(inspection.Team.Repo, entry, gatePr is not null).ConfigureAwait(false);
        if (git.IsError)
        {
            return git.Error;
        }

        var changed = await ChangedSince(inspection, entry, gatePr, pull).ConfigureAwait(false);
        if (changed.IsError)
        {
            return changed.Error;
        }

        var ahead = await AheadOfPrHead(inspection.Team.Repo, entry, gatePr is null ? pull : null).ConfigureAwait(false);
        var composed = Compose(inspection, entry, gatePr, pull, git.Value with { Changed = changed.Value, Ahead = ahead });
        return await WithBugfix(inspection, entry, composed).ConfigureAwait(false);
    }

    // The time rule 7 compares a tree with: the later of the last sweep's start and (for a tree whose pull request merged or closed) when it ended; null when there is nothing to compare with.
    private static DateTimeOffset? Cutoff(Inspection inspection, int? gatePr, PrBranch? pull) =>
        gatePr is null && pull is { State: PrState.Merged or PrState.Closed } ? (pull.EndedAt is { } ended ? inspection.Since is { } since && since > ended ? since : ended : null) : inspection.Since;

    // Whether anything in the tree changed after the cutoff. A gate tree is aw-meta's own and is exempt; so is a missing directory (no files to read).
    private async ValueTask<Result<bool>> ChangedSince(Inspection inspection, WorktreeEntry entry, int? gatePr, PrBranch? pull)
    {
        if (Cutoff(inspection, gatePr, pull) is not { } since || gatePr is not null || entry.Prunable)
        {
            return false;
        }

        var skip = inspection.Team.Worktrees.EffectiveBuildOutputs.Append(".git").ToList();
        var newest = await files.NewestChange(entry.Path, skip, inspection.Cancellation).ConfigureAwait(false);
        return newest.IsError ? newest.Error : newest.Value > since;
    }

    // How many commits HEAD has that the pull request's recorded head lacks (#2659). A tree that only lags behind that head (it never
    // pulled the last push) has none. Null when git can't tell, for example when that head is not a local object. Only an ended pull request's branch tree asks (a gate tree passes null).
    private async ValueTask<int?> AheadOfPrHead(string directory, WorktreeEntry entry, PrBranch? pull)
    {
        if (pull is not { State: PrState.Merged or PrState.Closed, Head.Length: > 0 } || string.Equals(pull.Head, entry.Head, StringComparison.Ordinal))
        {
            return 0;
        }

        var ahead = await repo.Git(directory, "rev-list", "--count", pull.Head + ".." + entry.Head).ConfigureAwait(false);
        return ahead.IsSuccess && int.TryParse(ahead.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null;
    }

    // The git questions about one worktree; the first failure ends it (the worktree is then kept, unjudged).
    private async ValueTask<Result<GitFacts>> Gather(string directory, WorktreeEntry entry, bool gate)
    {
        var merged = await repo.Git(directory, "rev-list", "--count", entry.Head, "--not", "origin/main").ConfigureAwait(false);
        if (merged.IsError)
        {
            return merged.Error;
        }

        var unpushed = await repo.Git(directory, "rev-list", "--count", entry.Head, "--not", "--remotes").ConfigureAwait(false);
        if (unpushed.IsError || entry.Prunable)
        {
            return unpushed.IsError ? unpushed.Error : new GitFacts(Count(merged.Value), Count(unpushed.Value), string.Empty, entry.Path, string.Empty);
        }

        var work = await Work(directory, entry, gate).ConfigureAwait(false);
        return work.IsError ? work.Error : new GitFacts(Count(merged.Value), Count(unpushed.Value), work.Value.Status, work.Value.Physical, work.Value.Committer);
    }

    private async ValueTask<Result<(string Status, string Physical, string Committer)>> Work(string directory, WorktreeEntry entry, bool gate)
    {
        var status = await repo.Git(entry.Path, "status", "--porcelain", "--untracked-files=all").ConfigureAwait(false);
        var physical = status.IsError ? Results.Failure<string>(status.Error) : await repo.Git(entry.Path, "rev-parse", "--show-toplevel").ConfigureAwait(false);
        var committer = physical.IsError || !gate ? Results.Success(string.Empty) : await repo.Git(directory, "log", "-1", "--format=%ce", entry.Head).ConfigureAwait(false);
        return physical.IsError || committer.IsError
            ? physical.IsError ? physical.Error : committer.Error
            : (status.Value, physical.Value, committer.Value);
    }

    private static WorktreeFacts Compose(Inspection inspection, WorktreeEntry entry, int? gatePr, PrBranch? pull, GitFacts git)
    {
        var places = new[] { entry.Path, git.Physical }.Select(WorktreeRoots.Normalize).Distinct(StringComparer.Ordinal).ToList();
        return new WorktreeFacts
        {
            Path = entry.Path,
            Branch = entry.Branch,
            IsMainWorktree = entry.IsMain,
            IsSharedCheckout = places.Contains(WorktreeRoots.Normalize(inspection.Team.Repo), StringComparer.Ordinal),
            Locked = entry.Locked,
            LockReason = entry.LockReason,
            DirectoryMissing = entry.Prunable,
            UnderAllowedRoot = WorktreeRoots.Contains(inspection.Roots, entry.Path),
            HeadMerged = git.NotInMain == 0,
            UnpushedCommits = git.Unpushed,
            DirtyEntries = StatusPorcelain.Significant(git.Status, inspection.Team.Worktrees.EffectiveBuildOutputs),
            Occupants = [.. inspection.Scan.Where(p => places.Exists(place => p.Paths.Any(path => WorktreeRoots.Inside(place, path))))],
            ChangedSinceSweep = git.Changed,
            ChangedSinceEnd = gatePr is null && pull is { State: PrState.Merged or PrState.Closed, EndedAt: { } ended } && !(inspection.Since > ended),
            EndedAtUnknown = gatePr is null && pull is { State: PrState.Merged or PrState.Closed, EndedAt: null },
            SweepStartedAt = inspection.Since,
            NoPreviousSweep = inspection.Since is null,
            PrNumber = gatePr is null ? pull?.Number : null,
            PrState = pull?.State,
            Head = entry.Head,
            PrHead = HeadOf(pull),
            CommitsAfterPrHead = git.Ahead,
            Done = inspection.Scope.Done,
            KeepNoPr = !inspection.Scope.IncludeNoPr,
            GatePr = gatePr,
            HeadIsGateMerge = gatePr is not null && git.Committer == GateCommitter,
            GateRunning = gatePr is { } running && inspection.Scan.Any(p => !p.IsInvoker && Runs(p.Command, running)),
        };
    }

    private sealed record GitFacts(int NotInMain, int Unpushed, string Status, string Physical, string Committer, bool Changed = false, int? Ahead = 0);

    private static string HeadOf(PrBranch? pull) => pull?.Head ?? string.Empty;

    private static int Count(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 0;

    private static bool Same(string a, string b) => WorktreeRoots.Normalize(a) == WorktreeRoots.Normalize(b);

    // "aw-meta gate-run 7 ...", ".../net10.0/aw-meta merge-run #7": the process itself is aw-meta (installed, or the dev apphost, which
    // has the same name) with the verb and the pull request number as its first three words. A shell or grep whose command line only mentions them doesn't count.
    private static bool Runs(string command, int pr)
    {
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 3
            && string.Equals(words[0][(words[0].LastIndexOf('/') + 1)..], GateHost, StringComparison.Ordinal)
            && _gateCommands.Contains(words[1], StringComparer.Ordinal)
            && words[2].TrimStart('#') == pr.ToString(CultureInfo.InvariantCulture);
    }
}
