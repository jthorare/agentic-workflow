using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IInstalledBuilds over the current build's inputs/ folder, which scripts/install-cs.sh writes.</summary>
/// <param name="currentBuild">The current build: $AW_STATE_DIR/bin/cs/current.</param>
internal sealed class FileInstalledBuilds(string currentBuild) : IInstalledBuilds
{
    public async ValueTask<Result<InstalledBinary?>> Current(string binary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binary);
        var inputs = Path.Combine(currentBuild, "inputs");
        if (await Lines(Path.Combine(inputs, binary + ".rev"), cancellationToken).ConfigureAwait(false) is not [var revision, ..])
        {
            return Results.Success<InstalledBinary?>(value: null);
        }

        var specs = await Lines(Path.Combine(inputs, binary + ".paths"), cancellationToken).ConfigureAwait(false);
        return Results.Success<InstalledBinary?>(new InstalledBinary(revision, specs));
    }

    // The file's non-empty lines, trimmed; null when it is missing or can't be read (a build made before that record).
    private static async ValueTask<IReadOnlyList<string>?> Lines(string path, CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return [.. text.Split('\n').Select(static line => line.Trim()).Where(static line => line.Length > 0)];
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
