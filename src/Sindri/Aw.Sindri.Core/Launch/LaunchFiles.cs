using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>The names of the flat per-launch files under <c lang="text">$AW_SINDRI_HOME/launches/</c> (launcher L-9): shared by the launcher, 'session wrap' and the id hook.</summary>
public static class LaunchFiles
{
    /// <summary>The directory under Sindri's home.</summary>
    public const string Directory = "launches";

    /// <summary>The capture rule's file name for <paramref name="launch"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>'&lt;id&gt;.capture.json'.</returns>
    public static string CaptureName(LaunchId launch) => launch.Value + ".capture.json";

    /// <summary>The file the id hook writes for <paramref name="launch"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>'&lt;id&gt;.session-id'.</returns>
    public static string SessionIdName(LaunchId launch) => launch.Value + ".session-id";
}
