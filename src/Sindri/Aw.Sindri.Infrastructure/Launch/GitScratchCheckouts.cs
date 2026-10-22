using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary><see cref="IScratchCheckouts"/> as detached git worktrees: <c lang="text">git worktree add --detach &lt;path&gt; &lt;sha&gt;</c>, removed with <c lang="text">git worktree remove --force</c>.</summary>
internal sealed class GitScratchCheckouts : IScratchCheckouts
{
    // As LaunchWorktree: git killed mid-checkout leaves a locked half-checkout, so it ends only with the launch's own cancellation.
    private static readonly TimeSpan _gitBudget = Timeout.InfiniteTimeSpan;

    /// <inheritdoc/>
    public async ValueTask<Result<string>> Create(string repoPath, string path, string headSha, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(repoPath);
        if (!Placeholders.IsAbsolute(path))
        {
            return SindriErrors.RequestInvalid.With("the scratch checkout's path isn't absolute (fix: pass an absolute path)");
        }

        if (!IsSha(headSha))
        {
            return SindriErrors.RequestInvalid.With("the scratch checkout's commit isn't 7 to 64 hex digits (fix: pass the head sha)");
        }

        if (Directory.Exists(path) && await Head(path, cancellationToken).ConfigureAwait(false) is { } head && await Resolve(repoPath, headSha, cancellationToken).ConfigureAwait(false) == head)
        {
            return path;   // a relaunch finds its own checkout
        }

        if (Directory.Exists(path))
        {
            _ = await Remove(path, cancellationToken).ConfigureAwait(false);   // a stale one at another commit
        }

        try
        {
            if (await ViewerProcess.Output("git", ["-C", repoPath, "worktree", "add", "--detach", path, headSha], _gitBudget, cancellationToken).ConfigureAwait(false) is not null)
            {
                return path;
            }
        }
        catch (OperationCanceledException)
        {
            _ = await Remove(path, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        _ = await Remove(path, CancellationToken.None).ConfigureAwait(false);
        return SindriErrors.LaunchRepoMissing.With($"the scratch checkout of {headSha} could not be made at {path}");
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
        {
            return Unit.Value;
        }

        // The repo is found from the checkout itself, which is how a cleanup needs no repo path.
        if (await ViewerProcess.Output("git", ["-C", path, "rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken).ConfigureAwait(false) is { Length: > 0 } common)
        {
            _ = await ViewerProcess.Output("git", ["--git-dir", common, "worktree", "remove", "--force", "--force", path], _gitBudget, cancellationToken).ConfigureAwait(false);
        }

        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, recursive: true);   // not a worktree any more, or never one: nothing else is left to keep
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return SindriErrors.LaunchRepoMissing.With($"the scratch checkout at {path} could not be removed: {e.Message}");
            }
        }

        return Unit.Value;
    }

    private static bool IsSha(string text) => text.Length is >= 7 and <= 64 && text.All(static c => char.IsAsciiHexDigit(c));

    private static ValueTask<string?> Head(string path, CancellationToken cancellationToken) =>
        ViewerProcess.Output("git", ["-C", path, "rev-parse", "HEAD"], cancellationToken);

    private static ValueTask<string?> Resolve(string repoPath, string sha, CancellationToken cancellationToken) =>
        ViewerProcess.Output("git", ["-C", repoPath, "rev-parse", "--verify", sha + "^{commit}"], cancellationToken);
}
