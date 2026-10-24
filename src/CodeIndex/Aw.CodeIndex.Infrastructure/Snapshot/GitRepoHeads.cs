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
/// the first of origin/HEAD, main, master that exists, local refs only and never a fetch.
/// </summary>
internal sealed class GitRepoHeads : IRepoHeads
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);
    private static readonly string[] _defaultRefs = ["refs/remotes/origin/HEAD", "refs/heads/main", "refs/heads/master"];

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

        var reaches = await Reaches(repo.Path, sha, cancellationToken).ConfigureAwait(false);
        return new RepoHead(repo.Name, sha, !reaches.IsError && reaches.Value.Contains);
    }

    /// <summary>Whether the first default ref that exists holds the commit; none existing is 'not on it'.</summary>
    private static async ValueTask<Result<(string Ref, bool Contains)>> Reaches(string repo, CommitSha commit, CancellationToken cancellationToken)
    {
        foreach (var name in _defaultRefs)
        {
            var resolved = await Run(repo, cancellationToken, "rev-parse", "--verify", "--quiet", name + "^{commit}").ConfigureAwait(false);
            if (resolved.IsError)
            {
                return resolved.Error;
            }

            if (resolved.Value.Exit != 0)
            {
                continue;   // no such ref: try the next
            }

            var ancestor = await Run(repo, cancellationToken, "merge-base", "--is-ancestor", commit.Value, resolved.Value.Stdout.Trim()).ConfigureAwait(false);
            return ancestor switch
            {
                { IsError: true } => ancestor.Error,
                { Value.Exit: 0 } => (name, true),
                { Value.Exit: 1 } => (name, false),
                _ => Failed("merge-base", ancestor.Value.Exit),
            };
        }

        return ("none", false);
    }

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
