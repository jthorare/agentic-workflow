using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Where the 'pr-watch' poller keeps what it saw last: '&lt;home&gt;/workflow/pr-watch.json' (PR babysitter spec §6.2).</summary>
internal interface IPrWatchState
{
    /// <summary>Reads the file; a missing one reads as empty.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state, or SND-SCHED-001 when the file can't be read.</returns>
    public ValueTask<Result<PrWatchFile>> Read(CancellationToken cancellationToken);

    /// <summary>Replaces the file whole.</summary>
    /// <param name="file">The state.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Done, or SND-SCHED-001 when the file can't be written.</returns>
    public ValueTask<Result<Unit>> Write(PrWatchFile file, CancellationToken cancellationToken);
}

/// <summary>The poller's state: the last digest of each pull request, and each host's back-off.</summary>
/// <param name="Pulls">By pull request link.</param>
/// <param name="Hosts">By host name; a host appears only while it is failing.</param>
internal sealed record PrWatchFile(IReadOnlyDictionary<string, PrWatchEntry> Pulls, IReadOnlyDictionary<string, PrWatchHost> Hosts)
{
    /// <summary>Nothing seen yet.</summary>
    public static PrWatchFile Empty { get; } = new(new Dictionary<string, PrWatchEntry>(StringComparer.Ordinal), new Dictionary<string, PrWatchHost>(StringComparer.Ordinal));
}

/// <summary>What the poller saw of one pull request the last time it changed (or first appeared).</summary>
/// <param name="Digest">The forge's version text.</param>
/// <param name="HeadSha">The head's tip.</param>
/// <param name="State">The pull request's state, by name ('Open', 'Closed', 'Merged').</param>
/// <param name="QuietSince">When the pull request last changed, or last reminded; the 'idle' period counts from here.</param>
internal sealed record PrWatchEntry(string Digest, string HeadSha, string State, DateTimeOffset QuietSince);

/// <summary>A failing host: how many polls in a row failed and when it is tried again.</summary>
/// <param name="Failures">Consecutive failed polls.</param>
/// <param name="Until">No poll of this host before this time.</param>
internal sealed record PrWatchHost(int Failures, DateTimeOffset Until);
