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
}
