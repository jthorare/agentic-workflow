using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The packages a profile repo's lockfiles pin: what a 'vulnerabilities' node with a 'repo' asks about.</summary>
public interface ILockfileSource
{
    /// <summary>Reads the lockfiles of a repo.</summary>
    /// <param name="repo">A repo of the approved profile.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The packages, or why they could not be read (no such repo).</returns>
    public ValueTask<Result<IReadOnlyList<VulnPackage>>> Read(string repo, CancellationToken cancellationToken);
}
