using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>What scripts/install-cs.sh recorded about each binary of the build it linked as current (bin/cs/current/inputs/).</summary>
public interface IInstalledBuilds
{
    /// <summary>Reads one binary's record in the current build.</summary>
    /// <param name="binary">The binary's name (judge, sindri).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record; null when there is no current build or it holds no record of that binary.</returns>
    public ValueTask<Result<InstalledBinary?>> Current(string binary, CancellationToken cancellationToken);
}
