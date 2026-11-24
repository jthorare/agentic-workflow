namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The build this control plane runs and the build the install points at (#1032): both are stamps, the folder names under 'bin/cs/' that scripts/install-cs.sh publishes into,
/// so a running server whose stamp is not the one 'bin/cs/current' names is stale. A read of the process's own folder and of the link, starting nothing.
/// </summary>
public interface IBuildStamps
{
    /// <summary>The stamp of the build this process runs from.</summary>
    /// <returns>The stamp, or null when this process isn't running from 'bin/cs/&lt;stamp&gt;' (a dev run).</returns>
    public string? Running();

    /// <summary>The stamp 'bin/cs/current' names.</summary>
    /// <returns>The stamp, or null when there is no link.</returns>
    public string? Installed();

    /// <summary>The running build folder's 'build-info' (first-run spec §3.2).</summary>
    /// <returns>Its commit and built time, or null in a dev run or when the install pruned the folder this process runs from.</returns>
    public BuildInfo? RunningBuild() => null;

    /// <summary>The 'build-info' of the build 'bin/cs/current' names.</summary>
    /// <returns>Its commit and built time, or null with no link or when that build has no 'build-info' (an unfinished install, SND-SETUP-011).</returns>
    public BuildInfo? InstalledBuild() => null;
}
