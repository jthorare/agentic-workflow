using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// Where a launch's sandbox files are, under the launcher's directory (<c lang="text">LauncherOptions.LaunchDirectory</c>, <c lang="text">&lt;home&gt;/launch</c>): the run area
/// 'areas/&lt;id&gt;', the stored plan 'areas/&lt;id&gt;/sandbox-plan.json' and the denial counter 'denials/&lt;id&gt;.json'. The writers (the run-area preparer, the exec,
/// the launch inputs) and the readers (the session and denial adapters) all ask here, so they cannot drift apart.
/// </summary>
internal static class LaunchPaths
{
    /// <summary>The run area's file name for the stored plan.</summary>
    internal const string PlanName = "sandbox-plan.json";

    /// <summary>The run directory's file name for the limits 'sindri session wrap --limits' reads (#1553).</summary>
    internal const string WrapLimitsName = "wrap-limits.json";

    /// <summary>The run area of a launch.</summary>
    /// <param name="launchDirectory">The launcher's directory.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The folder.</returns>
    internal static string RunArea(string launchDirectory, LaunchId launch) => Path.Combine(launchDirectory, "areas", launch.Value);

    /// <summary>The stored plan of a launch.</summary>
    /// <param name="launchDirectory">The launcher's directory.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The file.</returns>
    internal static string PlanFile(string launchDirectory, LaunchId launch) => Path.Combine(RunArea(launchDirectory, launch), PlanName);

    /// <summary>The folder of the denial counters.</summary>
    /// <param name="launchDirectory">The launcher's directory.</param>
    /// <returns>The folder.</returns>
    internal static string DenialsDirectory(string launchDirectory) => Path.Combine(launchDirectory, "denials");

    /// <summary>The denial counter of a launch.</summary>
    /// <param name="launchDirectory">The launcher's directory.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The file.</returns>
    internal static string DenialsFile(string launchDirectory, LaunchId launch) => Path.Combine(DenialsDirectory(launchDirectory), launch.Value + ".json");
}
