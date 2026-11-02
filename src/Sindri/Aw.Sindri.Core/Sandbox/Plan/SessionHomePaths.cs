using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Where a launch's session home lives (sandbox spec §3.1, rev 7): beside the real Sindri home, never inside it, so no variable or argv entry names a path under the real home.</summary>
public static class SessionHomePaths
{
    /// <summary>The folder that holds every launch's session home: &lt;real home&gt;-sessions.</summary>
    /// <param name="sindriHome">The real AW_SINDRI_HOME.</param>
    /// <returns>The path.</returns>
    public static string Sessions(string sindriHome)
    {
        ArgumentNullException.ThrowIfNull(sindriHome);
        return sindriHome + "-sessions";
    }

    /// <summary>One launch's session home: &lt;real home&gt;-sessions/&lt;launch&gt;.</summary>
    /// <param name="sindriHome">The real AW_SINDRI_HOME.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The path.</returns>
    public static string For(string sindriHome, LaunchId launch) => $"{Sessions(sindriHome)}/{launch.Value}";

    /// <summary>The agent GitHub token's folder in a session home (SB-7): bound read-only as a directory, so a refresh's rename is seen inside (a single-file bind keeps the old inode, R1-14).</summary>
    /// <param name="sessionHome">The launch's session home.</param>
    /// <returns>The path.</returns>
    public static string GhTokenDirectory(string sessionHome)
    {
        ArgumentNullException.ThrowIfNull(sessionHome);
        return sessionHome + "/gh-token";
    }

    /// <summary>The token file inside <see cref="GhTokenDirectory"/>; the gh shim and the git credential helper read it at each call.</summary>
    /// <param name="sessionHome">The launch's session home.</param>
    /// <returns>The path.</returns>
    public static string GhTokenFile(string sessionHome) => GhTokenDirectory(sessionHome) + "/token";
}
