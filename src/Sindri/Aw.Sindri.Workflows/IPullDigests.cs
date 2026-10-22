using Aw.Kernel;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The cheap read of many pull requests at once (PR babysitter spec §6.2) that the 'pr-watch' poller compares from poll to poll. The one adapter,
/// <see cref="ForgePullDigests"/>, is the only place that calls the forge port's digest member.
/// </summary>
internal interface IPullDigests
{
    /// <summary>Reads the digests of <paramref name="links"/>, any number of them (the adapter batches).</summary>
    /// <param name="links">The pull requests' links, as 'fact:pr' holds them.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per pull request the forge answered for, or the error (SND-FORGE-002 refused, SND-FORGE-004 unreachable or rate limited).</returns>
    public ValueTask<Result<IReadOnlyList<PullDigestRow>>> Digest(IReadOnlyList<string> links, CancellationToken cancellationToken);
}

/// <summary>One pull request's digest: equal digests mean nothing the poller watches has changed.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="Digest">The forge's version text for the pull request.</param>
/// <param name="State">Open, closed or merged.</param>
/// <param name="HeadSha">The head's tip.</param>
internal sealed record PullDigestRow(string Link, string Digest, PullState State, string HeadSha);
