using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>No pull request has a babysitter now that the PR watcher is gone, so a fix task is never routed to one.</summary>
internal sealed class NoFixBabysitters : IFixBabysitters
{
    public ValueTask<bool> Watching(string link, CancellationToken cancellationToken) => ValueTask.FromResult(false);
}
