using System.Diagnostics;
using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Snapshot;

/// <summary>
/// Where each checkout is (spec §6.1), read with git only: 'git rev-parse HEAD', then 'git merge-base --is-ancestor' against
/// the first of origin/HEAD, main, master, origin/main, origin/master that exists, local refs only and never a fetch. That ref's commit is the default tip, the commit the first index of a repo
/// that is off its default branch builds from (#3181).
/// </summary>
internal sealed class GitRepoHeads : IRepoHeads
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);
    private static readonly string[] _defaultRefs = ["refs/remotes/origin/HEAD", "refs/heads/main", "refs/heads/master", "refs/remotes/origin/main", "refs/remotes/origin/master"];

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<RepoHead>>> Heads(IReadOnlyList<CodeIndexRepo> repos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repos);
        var heads = new List<RepoHead>(repos.Count);
        foreach (var repo in repos)
        {
            heads.Add(await One(repo, cancellationToken).ConfigureAwait(false));
        }

        return heads;
    }

    /// <inheritdoc />
    public async ValueTask<Result<DefaultBranchCheck>> OnDefaultBranch(CodeIndexRepo repo, CommitSha commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var known = await Run(repo.Path, cancellationToken, "cat-file", "-e", commit.Value + "^{commit}").ConfigureAwait(false);
        if (known.IsError || known.Value.Exit != 0)
        {
            return CodeIndexErrors.RepoNotFound.With("the commit is not in the repo");
        }

        var reaches = await Reaches(repo.Path, commit, cancellationToken).ConfigureAwait(false);
        return reaches.IsError ? reaches.Error : new DefaultBranchCheck(reaches.Value.Ref, reaches.Value.Contains);
    }

    private static async ValueTask<RepoHead> One(CodeIndexRepo repo, CancellationToken cancellationToken)
    {
        var head = await Run(repo.Path, cancellationToken, "rev-parse", "--verify", "--quiet", "HEAD^{commit}").ConfigureAwait(false);
        if (head.IsError || head.Value.Exit != 0 || !CommitSha.TryFrom(head.Value.Stdout.Trim(), out var sha))
        {
            return new RepoHead(repo.Name, Head: null, OnDefaultBranch: false);
        }

        var tip = await DefaultTip(repo.Path, cancellationToken).ConfigureAwait(false);
        if (tip.IsError || tip.Value is not { } found)
        {
            return new RepoHead(repo.Name, sha, OnDefaultBranch: false);
        }

        var reaches = await Contains(repo.Path, sha, found, cancellationToken).ConfigureAwait(false);
        return new RepoHead(repo.Name, sha, !reaches.IsError && reaches.Value.Contains, found.Tip);
    }

    /// <summary>Whether the first default ref that exists holds the commit; none existing is 'not on it'.</summary>
    private static async ValueTask<Result<(string Ref, bool Contains)>> Reaches(string repo, CommitSha commit, CancellationToken cancellationToken)
    {
        var tip = await DefaultTip(repo, cancellationToken).ConfigureAwait(false);
        return tip.IsError ? tip.Error : tip.Value is { } found ? await Contains(repo, commit, found, cancellationToken).ConfigureAwait(false) : ("none", false);
    }

    /// <summary>The first default ref that exists and its commit, or null when the checkout has none of them.</summary>
    private static async ValueTask<Result<DefaultRef?>> DefaultTip(string repo, CancellationToken cancellationToken)
    {
        foreach (var name in _defaultRefs)
        {
            var resolved = await Run(repo, cancellationToken, "rev-parse", "--verify", "--quiet", name + "^{commit}").ConfigureAwait(false);
            if (resolved.IsError)
            {
                return resolved.Error;
            }

            if (resolved.Value.Exit == 0 && CommitSha.TryFrom(resolved.Value.Stdout.Trim(), out var tip))
            {
                return new DefaultRef(name, tip);
            }

            // no such ref: try the next
        }

        return Results.Success<DefaultRef?>(value: null);
    }

    private static async ValueTask<Result<(string Ref, bool Contains)>> Contains(string repo, CommitSha commit, DefaultRef found, CancellationToken cancellationToken)
    {
        var ancestor = await Run(repo, cancellationToken, "merge-base", "--is-ancestor", commit.Value, found.Tip.Value).ConfigureAwait(false);
        return ancestor switch
        {
            { IsError: true } => ancestor.Error,
            { Value.Exit: 0 } => (found.Name, true),
            { Value.Exit: 1 } => (found.Name, false),
            _ => Failed("merge-base", ancestor.Value.Exit),
        };
    }

    private sealed record DefaultRef(string Name, CommitSha Tip);

    private static async ValueTask<Result<(int Exit, string Stdout)>> Run(string repo, CancellationToken cancellationToken, params string[] args)
    {
        if (!Directory.Exists(repo))
        {
            return CodeIndexErrors.RepoNotFound.With("the repo path does not exist");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        Process? git = null;
        try
        {
            git = Process.Start(CleanGit.Prepare(repo, redirectInput: false, args));
            if (git is null)
            {
                return CodeIndexErrors.RepoNotFound.With("git did not start");
            }

            var stdout = git.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = git.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var text = await stdout.ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            return (git.ExitCode, text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Stop(git);
            return CodeIndexErrors.RepoNotFound.With($"git {args[0]} did not finish in time");
        }
        finally
        {
            git?.Dispose();
        }
    }

    private static void Stop(Process? git)
    {
        try
        {
            if (git is { HasExited: false })
            {
                git.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // it exited between the check and the kill
        }
    }

    private static Error Failed(string verb, int exit) =>
        CodeIndexErrors.RepoNotFound.With(string.Create(CultureInfo.InvariantCulture, $"git {verb} exited {exit}"));
}
