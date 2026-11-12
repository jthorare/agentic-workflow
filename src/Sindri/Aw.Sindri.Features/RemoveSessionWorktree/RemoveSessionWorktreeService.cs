using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.RemoveSessionWorktree;

/// <summary>Removes a one-off worktree (agents spec §15.8). Red stub: the ports join the constructor in step 2.</summary>
internal sealed class RemoveSessionWorktreeService : IService<RemoveSessionWorktreeRequest, RemovedWorktree>
{
    public ValueTask<Result<RemovedWorktree>> Handle(RemoveSessionWorktreeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: A7 step 2");
}
