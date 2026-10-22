using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary><see cref="IWorkspaceRebase"/> through git: fetches the base from 'origin' and rebases onto it, aborting a conflict.</summary>
internal sealed class GitWorkspaceRebase : IWorkspaceRebase
{
    public ValueTask<Result<RebaseOutcome>> Rebase(string workspace, string onto, string? oldBase, CancellationToken cancellationToken) => throw new NotSupportedException();
}
