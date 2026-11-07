using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>The fix-item closer for a host that registers the PR effects without the item commands: closes nothing, so a fix thread marked fixed leaves its item as it was.</summary>
internal sealed class NoFixItemCloser : IFixItemCloser
{
    public ValueTask<bool> Close(string item, CancellationToken cancellationToken) => ValueTask.FromResult(false);
}
