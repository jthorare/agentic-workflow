using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Extract;

/// <summary>Where a checkout came from, read from its git config only (never a fetch).</summary>
public interface IRepoRemote
{
    /// <summary>The fetch URL of the checkout's origin remote, as git prints it (git remote get-url origin).</summary>
    /// <param name="repo">The repo: its name and checkout.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The URL; null when the checkout has no origin; SND-CONTEXT-048 when the checkout can't be read.</returns>
    public ValueTask<Result<string?>> Origin(ContextRepo repo, CancellationToken cancellationToken);
}
