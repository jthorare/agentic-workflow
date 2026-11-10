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

    /// <summary>Finds the built binary of a toolkit: the newest file named after it under its bin folder.</summary>
    /// <param name="dir">The toolkit's folder.</param>
    /// <param name="name">The toolkit name.</param>
    /// <returns>The absolute path, or SND-TK-003 when the build left none.</returns>
    public Result<string> BinaryOf(string dir, string name);
}
