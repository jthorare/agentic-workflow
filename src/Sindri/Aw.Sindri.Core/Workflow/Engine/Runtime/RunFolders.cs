namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The run folder of an agent Step run (sandbox spec SB17; scoping station W11): outside the repository, one per run, so a rerun never sees a killed run's partial files. The engine writes the
/// Step's declared inputs into it before the launch, and a report's named outputs must resolve inside it.
/// </summary>
public static class RunFolders
{
    /// <summary>The folder of a run: '{home}/workflow/runs/{instance}/{step}/{run}'.</summary>
    /// <param name="home">The Sindri home ('AW_SINDRI_HOME').</param>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="step">The recipe state the Step runs in.</param>
    /// <param name="run">The run's token.</param>
    /// <returns>The absolute path.</returns>
    public static string PathFor(string home, WorkflowInstanceId instance, string step, RunToken run) =>
        Path.Combine(home, "workflow", "runs", instance.Value, step, run.Value);

    /// <summary>Whether a file name resolves inside the folder: a relative name whose '..' segments never leave it.</summary>
    /// <param name="folder">The run folder.</param>
    /// <param name="name">The name a report gives.</param>
    /// <returns>True when the name stays inside the folder.</returns>
    public static bool Contains(string folder, string name)
    {
        if (string.IsNullOrEmpty(name) || Path.IsPathRooted(name))
        {
            return false;
        }

        var root = Path.GetFullPath(folder);
        return Path.GetFullPath(Path.Combine(root, name)).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
