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
    private static readonly TimeSpan _limit = TimeSpan.FromMinutes(15);
    private CancellationToken _cancellation;

    /// <summary>The same calls, each stopped (its process tree killed, the cancellation thrown) when the token fires.</summary>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>A copy bound to the token.</returns>
    public RepoCommands Cancelling(CancellationToken cancellationToken) => new(runner, paths) { _cancellation = cancellationToken };

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

    /// <summary>Lists the open and recently merged pull requests.</summary>
    /// <param name="repo">The repo directory.</param>
    /// <returns>The pull requests, or an error.</returns>
    public async ValueTask<Result<IReadOnlyList<PrView>>> List(string repo)
    {
        var json = await Gh(repo, "pr", "list", "--state", "all", "--limit", "50", "--json", "number,state,isDraft,headRefOid,mergeable,title,updatedAt").ConfigureAwait(false);
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
