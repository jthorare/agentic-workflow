using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>The one rule for a launch's session name in its host: 'snd-&lt;launch id&gt;' (API P10a addendum, 2026-10-16). The launcher names the session with it and the launcher service addresses it with it.</summary>
public static class LaunchSessionName
{
    /// <summary>The name of <paramref name="launch"/>'s session.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The name.</returns>
    public static string For(LaunchId launch) => "snd-" + launch.Value;
}
