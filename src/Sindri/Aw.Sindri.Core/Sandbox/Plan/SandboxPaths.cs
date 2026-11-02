using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The paths of one launch.</summary>
/// <param name="Repo">The repo checkout.</param>
/// <param name="GitCommonDir">The repo's git directory (the common one).</param>
/// <param name="Worktree">The run's worktree (the cwd), or null for a session with none.</param>
/// <param name="RunDirectory">The run directory (SB17).</param>
/// <param name="RunArea">The launcher's per-run area: scratch, shims, hook spool directories, the plan.</param>
public sealed record SandboxPaths(string Repo, string GitCommonDir, string? Worktree, string RunDirectory, string RunArea)
{
    /// <summary>
    /// The launch's short temp directory on Seatbelt: '/private/tmp/aw-' and the first 8 hex digits of the launch id. macOS caps a unix socket path (sun_path) at 104 bytes, and .NET puts its named pipes
    /// (MSBuild nodes, the build server) at '$TMPDIR/CoreFxPipe_&lt;name&gt;', so TMPDIR in the run area's tmp (about 96 bytes for /Users/joi) leaves no room for the pipe name.
    /// </summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The directory.</returns>
    public static string SeatbeltTemp(LaunchId launch) => "/private/tmp/aw-" + launch.Value["LCH-".Length..][..8];
}
