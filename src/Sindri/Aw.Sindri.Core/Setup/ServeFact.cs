namespace Aw.Sindri.Core.Setup;

/// <summary>Whether the running Sindri is the installed build (first-run spec §3.2), with each build's commit and built time when its 'build-info' reads.</summary>
/// <param name="Current">True when serve runs the installed build, or when there is no stamp to compare (a dev run, a test).</param>
public sealed record ServeFact(bool Current)
{
    /// <summary>The running build's 'build-info'; null in a dev run or when the install pruned the running folder.</summary>
    public BuildInfo? Running { get; init; }

    /// <summary>The installed build's 'build-info'; null with no 'bin/cs/current' or no 'build-info' in it.</summary>
    public BuildInfo? Installed { get; init; }
}
