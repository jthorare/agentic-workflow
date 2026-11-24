using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IInstalledBuilds over the current build's inputs/ folder, which scripts/install-cs.sh writes.</summary>
/// <param name="currentBuild">The current build: $AW_STATE_DIR/bin/cs/current.</param>
internal sealed class FileInstalledBuilds(string currentBuild) : IInstalledBuilds
{
    public ValueTask<Result<InstalledBinary?>> Current(string binary, CancellationToken cancellationToken)
    {
        _ = (currentBuild, binary);
        throw new NotSupportedException("red: #4249 reads inputs/<binary>.rev and .paths");
    }
}
