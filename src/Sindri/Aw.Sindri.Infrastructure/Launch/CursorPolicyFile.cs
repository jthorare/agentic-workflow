using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The per-launch <c lang="text">.cursor/cli.json</c> behind the 'cursor-config' policy form (issue #1428): written 0600 into the Step's worktree and deleted
/// with the launch. Draft: the assumptions are at <see cref="Render"/>.
/// </summary>
internal static class CursorPolicyFile
{
    /// <summary>The file's text for <paramref name="policy"/>.</summary>
    /// <param name="policy">The Step's policy.</param>
    /// <returns>The JSON.</returns>
    internal static byte[] Render(StepPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return [];
    }

    /// <summary>Where the config lives in <paramref name="workDir"/>.</summary>
    /// <param name="workDir">The Step's worktree.</param>
    /// <returns>The file's path.</returns>
    internal static string PathIn(string workDir) => Path.Combine(workDir, ".cursor", "cli.json");

    /// <summary>The sidecar under the policy directory that remembers where the launch's file is, so the launch's cleanup can delete it without the worktree.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The sidecar's path.</returns>
    internal static string SidecarFor(string directory, LaunchId launch) => Path.Combine(directory, launch.Value + ".cursor-path");

    /// <summary>Writes the config 0600 and the sidecar, unless the worktree already has a <c lang="text">.cursor/cli.json</c> (it is the repo's, and is never overwritten).</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="workDir">The Step's worktree.</param>
    /// <param name="policy">The Step's policy.</param>
    /// <returns>The config's path, or null when the worktree already has one.</returns>
    internal static string? Write(string directory, LaunchId launch, string workDir, StepPolicy policy)
    {
        var path = PathIn(workDir);
        if (File.Exists(path))
        {
            return null;
        }

        OwnerOnly.EnsureDirectory(directory);
        OwnerOnly.EnsureDirectory(Path.GetDirectoryName(path) ?? workDir);
        OwnerOnly.WriteAtomically(path, Render(policy));
        OwnerOnly.WriteAtomically(SidecarFor(directory, launch), System.Text.Encoding.UTF8.GetBytes(path));
        return path;
    }

    /// <summary>Deletes the launch's config (the one its sidecar names) and the sidecar; missing ones are fine.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    internal static void Delete(string directory, LaunchId launch)
    {
        var sidecar = SidecarFor(directory, launch);
        try
        {
            if (File.Exists(sidecar))
            {
                _ = OwnerOnly.TryDelete(File.ReadAllText(sidecar).Trim());
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // the sidecar goes below; a config that can't be read back is left for the worktree's removal
        }

        _ = OwnerOnly.TryDelete(sidecar);
    }
}
