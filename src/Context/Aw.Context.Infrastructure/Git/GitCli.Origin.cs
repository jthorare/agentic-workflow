using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

/// <summary>The call behind the origin check: where a checkout was cloned from.</summary>
internal sealed partial class GitCli
{
    /// <summary>The fetch URL of the checkout's origin remote, read from its git config only (no fetch, no network).</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The URL as git prints it; null when the checkout has no origin remote; SND-CONTEXT-010 when git can't answer.</returns>
    public async ValueTask<Result<string?>> OriginUrl(string repo, CancellationToken cancellationToken)
    {
        var run = await Run(repo, ["remote", "get-url", "origin"], cancellationToken).ConfigureAwait(false);
        if (run.IsError)
        {
            return run.Error;
        }

        if (run.Value.Exit == 2)
        {
            return (string?)null;   // "error: No such remote 'origin'"
        }

        return run.Value.Exit == 0 ? run.Value.Stdout.Trim() is { Length: > 0 } url ? url : null : Failed("remote get-url", run.Value.Exit);
    }
}
