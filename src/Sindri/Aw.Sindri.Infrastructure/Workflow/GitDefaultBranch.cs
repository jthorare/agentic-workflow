using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary><see cref="IDefaultBranch"/> through git: <c language="text">git symbolic-ref --short refs/remotes/origin/HEAD</c> with the 'origin/' stripped.</summary>
internal sealed class GitDefaultBranch : IDefaultBranch
{
    public ValueTask<Result<string>> Of(string checkout, CancellationToken cancellationToken) => throw new NotSupportedException();
}
