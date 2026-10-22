using Aw.Kernel;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IPullDigests"/> over the forge port: the single caller of 'IPullRequests.Digest', whose rows ('PullDigestView') it maps to
/// <see cref="PullDigestRow"/>. The port batches at 20 or fewer, so it passes every link.
/// </summary>
/// <param name="pulls">The forge port.</param>
internal sealed class ForgePullDigests(IPullRequests pulls) : IPullDigests
{
    public async ValueTask<Result<IReadOnlyList<PullDigestRow>>> Digest(IReadOnlyList<string> links, CancellationToken cancellationToken)
    {
        var read = await pulls.Digest(links, cancellationToken).ConfigureAwait(false);
        return read.IsError
            ? read.Error
            : Results.Success<IReadOnlyList<PullDigestRow>>([.. read.Value.Select(static v => new PullDigestRow(v.Link, v.State, v.HeadSha, v.Checks, v.CommentCount, v.Version))]);
    }
}
