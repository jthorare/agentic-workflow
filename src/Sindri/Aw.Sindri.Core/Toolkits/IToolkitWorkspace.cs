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

    /// <summary>Finds the built binary of a toolkit: the apphost named by the folder's one *.Toolkit.csproj, the newest under its bin folder.</summary>
    /// <param name="dir">The toolkit's folder.</param>
    /// <returns>The absolute path, or SND-TK-003 when the build left none.</returns>
    public Result<string> BinaryOf(string dir);

    /// <summary>Finds the toolkit's tests project: the one *.Toolkit.Tests.csproj in a folder right under the toolkit's folder, which the scaffold writes and which references the toolkit project, so building it builds both.</summary>
    /// <param name="dir">The toolkit's folder.</param>
    /// <returns>The project's path relative to <paramref name="dir"/> with forward slashes, or SND-TK-002 when there is none or more than one.</returns>
    public Result<string> TestsProjectOf(string dir);
}
