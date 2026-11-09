using Aw.Kernel;

namespace Aw.Sindri.Core.Toolkits;

/// <summary>What Sindri reads from a toolkit's folder on disk.</summary>
public interface IToolkitWorkspace
{
    /// <summary>Hashes the toolkit's source files (not its bin and obj folders).</summary>
    /// <param name="dir">The toolkit's folder.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The hash as lowercase hex, or an error when the folder can't be read.</returns>
    public ValueTask<Result<string>> SourceHash(string dir, CancellationToken cancellationToken);

    /// <summary>Reads the manifest text (toolkit.json) the build left in the folder.</summary>
    /// <param name="dir">The toolkit's folder.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text, or SND-TK-003 when the file is missing.</returns>
    public ValueTask<Result<string>> ReadManifest(string dir, CancellationToken cancellationToken);
}
