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
}
