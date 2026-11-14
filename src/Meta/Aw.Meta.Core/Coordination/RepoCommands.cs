using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Coordination;

/// <summary>The git and gh calls the gate, merge and watch make against the team's repo. Every call goes through ICommandRunner; a non-zero exit is AW-META-012.</summary>
/// <param name="runner">Runs the programs.</param>
/// <param name="paths">Names the gh program.</param>
public sealed class RepoCommands(ICommandRunner runner, MetaPaths paths)
{
    // `stack{number}` is non-null for a pull request in a stack (gh stack); it is the only stack fact the merge needs.
    private const string QueueQuery = "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){pullRequest(number:$number){isMergeQueueEnabled isInMergeQueue mergeQueueEntry{position state} stack{number} timelineItems(last:1,itemTypes:[REMOVED_FROM_MERGE_QUEUE_EVENT]){nodes{... on RemovedFromMergeQueueEvent{reason}}}}}}";

    // The same query for a GitHub host whose schema has no `stack` field yet: such a host has no stacked pull requests.
    private const string QueueQueryWithoutStack = "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){pullRequest(number:$number){isMergeQueueEnabled isInMergeQueue mergeQueueEntry{position state} timelineItems(last:1,itemTypes:[REMOVED_FROM_MERGE_QUEUE_EVENT]){nodes{... on RemovedFromMergeQueueEvent{reason}}}}}}";

    private TimeSpan _limit = TimeSpan.FromMinutes(15);
    private CancellationToken _cancellation;

    /// <summary>The same calls, each stopped (its process tree killed, the cancellation thrown) when the token fires.</summary>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>A copy bound to the token.</returns>
    public RepoCommands Cancelling(CancellationToken cancellationToken) => new(runner, paths) { _cancellation = cancellationToken, _limit = _limit };

    /// <summary>The same calls, each given at most <paramref name="limit"/> (the default is 15 minutes, which suits a gate step and not a poll of GitHub). A call that runs past it is killed and fails with AW-META-012 "timed out".</summary>
    /// <param name="limit">The longest one call may run.</param>
    /// <returns>A copy with the limit.</returns>
    public RepoCommands Limited(TimeSpan limit) => new(runner, paths) { _cancellation = _cancellation, _limit = limit };

    /// <summary>Runs git in a directory.</summary>
    /// <param name="cwd">The working directory.</param>
    /// <param name="args">The arguments after "git".</param>
    /// <returns>The trimmed output, or AW-META-012 / AW-META-009.</returns>
    public ValueTask<Result<string>> Git(string cwd, params string[] args) => Run(cwd, ["git", .. args]);

    /// <summary>Runs gh in a directory.</summary>
    /// <param name="cwd">The working directory.</param>
    /// <param name="args">The arguments after "gh".</param>
    /// <returns>The trimmed output, or AW-META-012 / AW-META-009.</returns>
    public ValueTask<Result<string>> Gh(string cwd, params string[] args) => Run(cwd, [paths.Gh, .. args]);

    /// <summary>Reads a pull request from GitHub.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <param name="pr">The pull request number.</param>
    /// <param name="fields">The --json fields (headRefOid, headRefName, isDraft, title, state).</param>
    /// <returns>The pull request, or an error.</returns>
    public async ValueTask<Result<PrDetails>> View(string repo, int pr, string fields)
    {
        var json = await Gh(repo, "pr", "view", Number(pr), "--json", fields).ConfigureAwait(false);
        return json.IsError ? json.Error : GhJson.View(json.Value) is { } view ? view : Unexpected("gh pr view");
    }

    /// <summary>Reads what a CI gate needs of a pull request: its head, base branch, review decision and merge state.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <param name="pr">The pull request number.</param>
    /// <returns>The state, or an error.</returns>
    public async ValueTask<Result<PrCiState>> CiView(string repo, int pr)
    {
        var json = await Gh(repo, "pr", "view", Number(pr), "--json", "headRefOid,baseRefName,reviewDecision,mergeStateStatus").ConfigureAwait(false);
        return json.IsError ? json.Error : GhJson.CiView(json.Value) is { } view ? view : Unexpected("gh pr view");
    }

    /// <summary>Reads whether the base branch has a merge queue, where a pull request stands in it and whether it is part of a stack. gh 2.45 has no JSON field for it, so this asks GraphQL; gh fills the owner and repo from the repo in the directory. A host that answers "Field 'stack' doesn't exist" is asked again without that field and reads as not stacked.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <param name="pr">The pull request number.</param>
    /// <returns>The queue facts, or an error.</returns>
    public async ValueTask<Result<MergeQueueView>> QueueView(string repo, int pr)
    {
        var json = await Gh(repo, "api", "graphql", "-F", "owner={owner}", "-F", "name={repo}", "-F", "number=" + Number(pr), "-f", "query=" + QueueQuery).ConfigureAwait(false);
        if (json.IsError && json.Error.Message.Contains("'stack'", StringComparison.Ordinal) && json.Error.Message.Contains("exist", StringComparison.Ordinal))
        {
            json = await Gh(repo, "api", "graphql", "-F", "owner={owner}", "-F", "name={repo}", "-F", "number=" + Number(pr), "-f", "query=" + QueueQueryWithoutStack).ConfigureAwait(false);
        }

        return json.IsError ? json.Error : GhJson.QueueView(json.Value) is { } view ? view : Unexpected("gh api graphql");
    }

    /// <summary>Reads a pull request's required checks. gh exits 1 when a check failed and 8 when one is pending, and still prints them, so those exits are not errors here. A base with no required checks gives an empty list (gh prints "no required checks reported" and exits 1). Older gh has no --json for this command: the plain output is read instead.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <param name="pr">The pull request number.</param>
    /// <returns>The checks (empty when there are none), or an error.</returns>
    public async ValueTask<Result<IReadOnlyList<RequiredCheck>>> RequiredChecks(string repo, int pr)
    {
        var json = await Checks(repo, pr, "--json", "name,state,bucket").ConfigureAwait(false);
        if (json.IsError || !json.Value.Text.Contains("unknown flag: --json", StringComparison.Ordinal))
        {
            return json.IsError ? json.Error : Read(json.Value.Text, json.Value.Out, GhJson.Checks);
        }

        var plain = await Checks(repo, pr).ConfigureAwait(false);
        return plain.IsError ? plain.Error : Read(plain.Value.Text, plain.Value.Out, GhJson.ChecksFromText);
    }

    private async ValueTask<Result<(string Out, string Text)>> Checks(string repo, int pr, params string[] extra)
    {
        string[] argv = [paths.Gh, "pr", "checks", Number(pr), "--required", .. extra];
        var ran = await runner.Run(new CommandRequest(argv, _limit) { Cwd = repo }, _cancellation).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        var result = ran.Value;
        return result.TimedOut || result.ExitCode is not (0 or 1 or 8)
            ? MetaErrors.CommandFailed.With($"{string.Join(' ', argv.Take(4))}: {(result.TimedOut ? "timed out" : FirstLine(result.Stderr.Length > 0 ? result.Stderr : result.Stdout))}")
            : (result.Stdout, result.Stderr + "\n" + result.Stdout);
    }

    private static Result<IReadOnlyList<RequiredCheck>> Read(string text, string stdout, Func<string, IReadOnlyList<RequiredCheck>?> parse) =>
        string.IsNullOrWhiteSpace(stdout)
            ? text.Contains("no required checks", StringComparison.OrdinalIgnoreCase) ? Results.Success<IReadOnlyList<RequiredCheck>>([]) : Unexpected("gh pr checks")
            : parse(stdout) is { } checks ? Results.Success(checks) : Unexpected("gh pr checks");

    /// <summary>Lists the open and recently merged pull requests.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <returns>The pull requests, or an error.</returns>
    public async ValueTask<Result<IReadOnlyList<PrView>>> List(string repo)
    {
        var json = await Gh(repo, "pr", "list", "--state", "all", "--limit", "50", "--json", "number,state,isDraft,headRefOid,mergeable,title,updatedAt,headRefName,baseRefName").ConfigureAwait(false);
        return json.IsError ? json.Error : GhJson.List(json.Value) is { } list ? Results.Success(list) : Unexpected("gh pr list");
    }

    /// <summary>Lists pull requests in every state with their head branches (the newest 200).</summary>
    /// <param name="repo">The repo directory.</param>
    /// <returns>The pull requests, or an error.</returns>
    public async ValueTask<Result<IReadOnlyList<PrBranch>>> Branches(string repo)
    {
        var json = await Gh(repo, "pr", "list", "--state", "all", "--limit", "200", "--json", "number,state,headRefName,headRefOid,mergedAt,closedAt").ConfigureAwait(false);
        return json.IsError ? json.Error : GhJson.Branches(json.Value) is { } list ? Results.Success(list) : Unexpected("gh pr list");
    }

    /// <summary>Fetches origin, and the pull request's head when one is given.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <param name="pr">The pull request number, or null for origin only.</param>
    /// <returns>Success, or an error.</returns>
    public async ValueTask<Result<Unit>> Fetch(string repo, int? pr)
    {
        var origin = await Git(repo, "fetch", "--quiet", "origin").ConfigureAwait(false);
        if (origin.IsError || pr is null)
        {
            return origin.IsError ? origin.Error : Results.Ok();
        }

        var head = await Git(repo, "fetch", "--quiet", "origin", string.Create(CultureInfo.InvariantCulture, $"pull/{pr}/head")).ConfigureAwait(false);
        return head.IsError ? head.Error : Results.Ok();
    }

    /// <summary>Resolves a revision to its sha.</summary>
    /// <param name="cwd">A directory of the repo.</param>
    /// <param name="revision">The revision.</param>
    /// <returns>The sha, or an error.</returns>
    public ValueTask<Result<string>> Sha(string cwd, string revision) => Git(cwd, "rev-parse", "--verify", "--quiet", revision + "^{commit}");

    /// <summary>The tree "git merge-tree --write-tree" gives for merging a head onto a main commit, without touching any checkout.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <param name="main">The main commit.</param>
    /// <param name="head">The head to merge onto it.</param>
    /// <returns>The tree sha, or null when the merge conflicts or git can't tell.</returns>
    public async ValueTask<string?> MergeTree(string repo, string main, string head)
    {
        var merged = await Git(repo, "merge-tree", "--write-tree", main, head).ConfigureAwait(false);
        var first = merged.IsError ? null : merged.Value.Split('\n', 2)[0].Trim();
        return string.IsNullOrEmpty(first) ? null : first;
    }

    /// <summary>Whether merging a head onto a main commit conflicts (merge-tree exits 1), as opposed to git failing (any other non-zero exit).</summary>
    /// <param name="repo">The repo directory.</param>
    /// <param name="main">The main commit.</param>
    /// <param name="head">The head to merge onto it.</param>
    /// <returns>True when it conflicts, false when it merges cleanly, an error when git could not tell.</returns>
    public async ValueTask<Result<bool>> Conflicts(string repo, string main, string head)
    {
        var ran = await runner.Run(new CommandRequest(["git", "merge-tree", "--write-tree", main, head], _limit) { Cwd = repo }, _cancellation).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        var result = ran.Value;
        return result is { TimedOut: false, ExitCode: 0 or 1 }
            ? result.ExitCode == 1
            : MetaErrors.CommandFailed.With($"git merge-tree: {(result.TimedOut ? "timed out" : FirstLine(result.Stderr.Length > 0 ? result.Stderr : result.Stdout))}");
    }

    /// <summary>The paths a range changes.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <param name="range">A git range: "origin/main...abc" or "abc..def".</param>
    /// <returns>The changed paths, or an error.</returns>
    public async ValueTask<Result<IReadOnlyList<string>>> Changed(string repo, string range)
    {
        var names = await Git(repo, "diff", "--name-only", range).ConfigureAwait(false);
        return names.IsError ? names.Error : Results.Success<IReadOnlyList<string>>([.. names.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]);
    }

    /// <summary>The sha origin's main branch points at now.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <returns>The sha, or an error.</returns>
    public async ValueTask<Result<string>> RemoteMain(string repo)
    {
        var line = await Git(repo, "ls-remote", "origin", "main").ConfigureAwait(false);
        return line.IsError ? line.Error : GhJson.RemoteSha(line.Value) is { } sha ? sha : Unexpected("git ls-remote");
    }

    /// <summary>The commit a merged pull request created on main.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <param name="pr">The pull request number.</param>
    /// <returns>The sha, or an error (also when the pull request has no merge commit).</returns>
    public async ValueTask<Result<string>> MergeCommit(string repo, int pr)
    {
        var json = await Gh(repo, "pr", "view", Number(pr), "--json", "mergeCommit").ConfigureAwait(false);
        return json.IsError ? json.Error : GhJson.MergeCommit(json.Value) is { } sha ? sha : Unexpected("gh pr view --json mergeCommit");
    }

    private async ValueTask<Result<string>> Run(string cwd, IReadOnlyList<string> argv)
    {
        var ran = await runner.Run(new CommandRequest(argv, _limit) { Cwd = cwd }, _cancellation).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        var result = ran.Value;
        return result.ExitCode == 0 && !result.TimedOut
            ? result.Stdout.Trim()
            : MetaErrors.CommandFailed.With($"{string.Join(' ', argv.Take(3))}: {(result.TimedOut ? "timed out" : FirstLine(result.Stderr.Length > 0 ? result.Stderr : result.Stdout))}");
    }

    private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "exit non-zero";

    private static string Number(int pr) => pr.ToString(CultureInfo.InvariantCulture);

    private static Error Unexpected(string what) => MetaErrors.CommandFailed.With(what + " printed something unexpected");
}
