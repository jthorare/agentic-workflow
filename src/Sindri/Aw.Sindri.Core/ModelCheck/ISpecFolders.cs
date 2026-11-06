using Aw.Kernel;

namespace Aw.Sindri.Core.ModelCheck;

/// <summary>Lists the spec folders of a workspace and reads their entries, without following links.</summary>
public interface ISpecFolders
{
    /// <summary>The folders matching <paramref name="pattern"/>, in name order.</summary>
    /// <param name="workspace">The directory the pattern is relative to.</param>
    /// <param name="pattern">The folder glob ('specs/tla/*/').</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The folders; an error when the workspace cannot be read.</returns>
    public ValueTask<Result<IReadOnlyList<SpecFolder>>> List(string workspace, string pattern, CancellationToken cancellationToken);

    /// <summary>Whether a workspace-relative path exists (a file or a folder), without following a link out of the workspace; the 'models:' lines of a README are checked with it (MDL-SPEC-002).</summary>
    /// <param name="workspace">The directory the path is relative to.</param>
    /// <param name="path">The repo-relative path, without a ':line'; a trailing '/' names a folder.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>True when it exists.</returns>
    public ValueTask<bool> Exists(string workspace, string path, CancellationToken cancellationToken);
}
