using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Map;

/// <summary>
/// Where a repo's code index build stands (issue 2945). Context only asks; the host that runs the index jobs answers, so Context needs no reference to it.
/// </summary>
public interface ICodeIndexBuildState
{
    /// <summary>Reads the build state of <paramref name="repo"/> now.</summary>
    /// <param name="repo">The profile repo.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The state; <see cref="CodeIndexBuildStatus.Idle"/> when nothing is known or nothing runs.</returns>
    public ValueTask<CodeIndexBuildStatus> Read(ContextRepoName repo, CancellationToken cancellationToken);
}
