using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Where a sandboxed session the owner opens works (sandbox spec §3.9): its own worktree, its ref namespace and its run directory, all named by the launch.</summary>
internal static class AttendedOpen
{
    /// <summary>The worktree beside the Steps': never the owner's main checkout (SB5 keeps the git directory protected).</summary>
    /// <param name="repoPath">The repo checkout.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The worktree path.</returns>
    public static string Worktree(string repoPath, LaunchId launch) => $"{repoPath.TrimEnd('/')}/.claude/worktrees/sindri-open-{launch.Value}";

    /// <summary>The branch namespace the session may write refs in.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The namespace.</returns>
    public static string RunNamespace(LaunchId launch) => $"sindri/open/{launch.Value}";

    /// <summary>The branch its worktree is made on, inside <see cref="RunNamespace"/> so the session can commit on it.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The branch name.</returns>
    public static string Branch(LaunchId launch) => RunNamespace(launch) + "/work";

    /// <summary>The directory for the plan and egress.log.</summary>
    /// <param name="sindriHome">Sindri's own home, or null.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The path, or null without a home.</returns>
    public static string? RunDirectory(string? sindriHome, LaunchId launch) => sindriHome is null ? null : $"{sindriHome.TrimEnd('/')}/open/{launch.Value}";
}
