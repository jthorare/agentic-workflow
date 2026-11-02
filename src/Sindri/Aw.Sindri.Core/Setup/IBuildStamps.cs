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
}
