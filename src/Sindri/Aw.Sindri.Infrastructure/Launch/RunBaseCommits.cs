using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The commit a run's worktree started from (sandbox spec §3.8), recorded once at the run's first launch and read back on every re-launch (a retry on a stronger model has another launch id, the same
/// instance and run): the engine's <see cref="LaunchRequest.BaseCommit"/> when it passes one, else the HEAD of the worktree this launch just created. A re-launch never re-reads HEAD, since the agent
/// may have committed a lockfile edit since.
/// </summary>
internal static class RunBaseCommits
{
    /// <summary>The run's base commit, recorded now when this is its first launch; null when none is known (the worktree already existed and the engine named no commit).</summary>
    /// <param name="request">The launch.</param>
    /// <param name="directory">Where the records are kept.</param>
    /// <param name="workDir">The worktree.</param>
    /// <param name="created">Whether this launch created the worktree.</param>
    /// <param name="cancellationToken">Cancels the read of HEAD.</param>
    /// <returns>The commit, or null.</returns>
    internal static async ValueTask<string?> Resolve(LaunchRequest request, string directory, string workDir, bool created, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, "base-" + KeyOf(request) + ".sha");
        try
        {
            if (File.Exists(path) && (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim() is { Length: > 0 } recorded)
            {
                return recorded;
            }

            var first = request.BaseCommit ?? (created ? await ViewerProcess.Output("git", ["-C", workDir, "rev-parse", "HEAD"], cancellationToken).ConfigureAwait(false) : null);
            if (string.IsNullOrWhiteSpace(first))
            {
                return null;
            }

            OwnerOnly.EnsureDirectory(directory);
            OwnerOnly.WriteAtomically(path, Encoding.UTF8.GetBytes(first));
            return first;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return request.BaseCommit;   // a record that can't be kept still serves this launch
        }
    }

    private static string KeyOf(LaunchRequest request)
    {
        var run = request.Instance is { } instance && request.Run is { } token ? instance.Value + ":" + token.Value : request.Launch.Value;
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(run)))[..24];
    }
}
