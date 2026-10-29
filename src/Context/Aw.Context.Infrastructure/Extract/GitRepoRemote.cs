using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>A checkout's origin URL from its git config: never fetches and never touches the working tree; a checkout git can't read is SND-CONTEXT-048, one with no origin has none (null).</summary>
/// <param name="git">The read-only git calls.</param>
internal sealed class GitRepoRemote(GitCli git) : IRepoRemote
{
    public async ValueTask<Result<string?>> Origin(ContextRepo repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var url = await git.OriginUrl(repo.Path, cancellationToken).ConfigureAwait(false);
        return url.IsError
            ? ContextErrors.ExtractionUnreadable.With($"git can't read the origin of {repo.Name.Value} (fix: check the checkout)")
            : url;
    }
}
