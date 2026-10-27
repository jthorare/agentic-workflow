using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Reads a repo's files at a commit through git (never the working tree), leaving out the paths the repo denies: they are not listed and reading one is refused like a missing path. Any git failure is SND-CONTEXT-048.</summary>
/// <param name="git">The read-only git calls.</param>
internal sealed class GitRepoReader(GitCli git) : IRepoReader
{
    public async ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var head = await git.Head(repo.Path, cancellationToken).ConfigureAwait(false);
        return head.IsError ? Unreadable(repo, "HEAD") : head;
    }

    public async ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var tree = await git.Tree(repo.Path, commit, cancellationToken).ConfigureAwait(false);
        return tree.IsError ? Unreadable(repo, commit) : Results.Success<IReadOnlyList<string>>([.. tree.Value.Keys.Where(p => !repo.Denies(p)).Order(StringComparer.Ordinal)]);
    }

    public async ValueTask<Result<IReadOnlyList<string>>> ListTracked(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var tree = await git.Tree(repo.Path, commit, cancellationToken).ConfigureAwait(false);
        return tree.IsError ? Unreadable(repo, commit) : Results.Success<IReadOnlyList<string>>([.. tree.Value.Keys.Order(StringComparer.Ordinal)]);
    }

    public async ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0 || Path.IsPathRooted(path) || path.Any(char.IsControl) || path.Split('/').Contains("..") || repo.Denies(path))
        {
            return Unreadable(repo, commit, "a path inside the repo");
        }

        var text = await git.Show(repo.Path, commit, path, cancellationToken).ConfigureAwait(false);
        return text.IsError ? Unreadable(repo, commit, path) : text;
    }

    public async ValueTask<Result<RepoChanges>> Changes(ContextRepo repo, DiffRange range, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(range);
        if (!range.IsValid())
        {
            return ContextErrors.QueryInvalid.With(DiffRange.Message);
        }

        var first = await git.Commit(repo.Path, range.Base, cancellationToken).ConfigureAwait(false);
        var second = first.IsError ? first : await git.Commit(repo.Path, range.Head, cancellationToken).ConfigureAwait(false);
        var mergeBase = second.IsError ? second : await git.MergeBase(repo.Path, first.Value, second.Value, cancellationToken).ConfigureAwait(false);
        var listed = mergeBase.IsError ? mergeBase.Error : await git.NameStatus(repo.Path, mergeBase.Value, second.Value, cancellationToken).ConfigureAwait(false);
        return listed.IsError
            ? Failure(repo, range, listed.Error)
            : new RepoChanges(first.Value, second.Value, mergeBase.Value, [.. listed.Value.OrderBy(c => c.Path, StringComparer.Ordinal)]);
    }

    // 110 and 111 are the caller's to fix and say which repo and range; any other git failure is the checkout (048). Neither prints what git said.
    private static Error Failure(ContextRepo repo, DiffRange range, Error error) =>
        error.Code == ContextErrors.CommitUnknown.Code
            ? ContextErrors.CommitUnknown.With($"the clone of {repo.Name.Value} holds no commit of {range}, or an abbreviation of it is ambiguous or spelled like a ref (fix: fetch that commit into the profile repo's checkout, or pass the full shas it holds)")
            : error.Code == ContextErrors.NoMergeBase.Code
                ? ContextErrors.NoMergeBase.With($"the commits of {range} share no merge base in {repo.Name.Value} (fix: pass a base on the head's history)")
                : Unreadable(repo, range.ToString());

    // Names the repo and the commit or path asked for, never what git printed (it may name repo content).
    private static Error Unreadable(ContextRepo repo, string what, string? path = null) =>
        ContextErrors.ExtractionUnreadable.With($"git can't read {path ?? what} of {repo.Name.Value} (fix: check the checkout and that the commit exists)");
}
