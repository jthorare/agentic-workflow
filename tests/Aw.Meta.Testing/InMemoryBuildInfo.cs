using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IBuildInfo: the one fake, kept honest by BuildInfoContract.</summary>
public sealed class InMemoryBuildInfo : IBuildInfo
{
    /// <summary>The stamp <see cref="Read"/> returns; change it to simulate another build.</summary>
    public BuildInfo Stamp { get; set; } = new("0123456789abcdef0123456789abcdef01234567", "2026-10-17T12:00:00Z", "Linux", "linux-x64");

    /// <inheritdoc />
    public BuildInfo Read() => Stamp;
}
