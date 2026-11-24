using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IInstalledBuilds: the one fake, kept honest by InstalledBuildsContract.</summary>
public sealed class InMemoryInstalledBuilds : IInstalledBuilds
{
    private readonly Dictionary<string, InstalledBinary> _binaries = new(StringComparer.Ordinal);

    /// <summary>Records a binary in the current build.</summary>
    /// <param name="binary">The binary's name.</param>
    /// <param name="record">Its revision and input pathspecs.</param>
    public void Put(string binary, InstalledBinary record) => _binaries[binary] = record;

    /// <inheritdoc />
    public ValueTask<Result<InstalledBinary?>> Current(string binary, CancellationToken cancellationToken) => Done.Of(_binaries.GetValueOrDefault(binary));
}
