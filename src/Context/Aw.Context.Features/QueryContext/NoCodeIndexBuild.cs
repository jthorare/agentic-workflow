using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.QueryContext;

/// <summary>The code index state of a host that runs no index jobs: nothing is ever being built.</summary>
internal sealed class NoCodeIndexBuild : ICodeIndexBuildState
{
    public ValueTask<CodeIndexBuildStatus> Read(ContextRepoName repo, CancellationToken cancellationToken) => ValueTask.FromResult(CodeIndexBuildStatus.Idle);
}
