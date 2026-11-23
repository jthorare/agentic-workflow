using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.SetupSources;

/// <summary>Build stamps a test sets: both null (a dev run) until it names them.</summary>
internal sealed class FixedBuildStamps : IBuildStamps
{
    /// <summary>The running stamp and its 'build-info'.</summary>
    public (string? Stamp, BuildInfo? Info) Runs { get; set; }

    /// <summary>The installed stamp and its 'build-info'.</summary>
    public (string? Stamp, BuildInfo? Info) Links { get; set; }

    public string? Running() => Runs.Stamp;

    public string? Installed() => Links.Stamp;

    public BuildInfo? RunningBuild() => Runs.Info;

    public BuildInfo? InstalledBuild() => Links.Info;
}
