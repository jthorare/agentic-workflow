using Aw.Kernel;

namespace Aw.Sindri.Core.Prove;

/// <summary>The files a phase's evidence children produced, read as hashes (BS-13): Sindri hashes them itself, so a proof never rests on a hash an agent typed.</summary>
public interface IArtifactFiles
{
    /// <summary>The SHA-256 of a file under the phase worktree.</summary>
    /// <param name="workspace">The phase worktree.</param>
    /// <param name="path">The file: relative to the worktree, or absolute inside it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>'sha256:' and 64 lowercase hex digits; null when the file is not there; an error when the path leaves the worktree or the file can't be read.</returns>
    public ValueTask<Result<string?>> Sha256(string workspace, string path, CancellationToken cancellationToken);
}
