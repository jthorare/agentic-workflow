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

/// <summary>One pull request's digest: the typed fields the poller classifies a change by, and an opaque version token.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="State">Open, closed or merged.</param>
/// <param name="HeadSha">The head's tip.</param>
/// <param name="Checks">The required checks as one word.</param>
/// <param name="CommentCount">How many comments the pull request has.</param>
/// <param name="Version">The forge's change token: compared for equality only, never parsed.</param>
internal sealed record PullDigestRow(string Link, PullState State, string HeadSha, PullChecksState Checks, int CommentCount, string Version);
