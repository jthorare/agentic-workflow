using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>Reads one kind of dependency manifest (the deps layer, spec §4.2). Text only: no restore, no network; a malformed file gives no rows, never an exception.</summary>
public interface IManifestReader
{
    /// <summary>Whether this reader reads the file.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>True for the manifest names the reader covers.</returns>
    public bool Handles(string path);

    /// <summary>Reads the dependencies a manifest declares.</summary>
    /// <param name="path">The repo-relative path; becomes each row's manifest.</param>
    /// <param name="text">The file's text.</param>
    /// <returns>The rows, raw (the build scrubs and cuts them), one per package name, at most <see cref="CodeIndexRules.MaxDepsPerManifest"/>.</returns>
    public IReadOnlyList<DepRecord> Read(string path, string text);
}
