using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The 'pr-watch' poller (PR babysitter spec §6.2), a built-in service every 60 s: lists the pull requests live instances wait on, reads their digests in one
/// call per host, compares each with the last in 'pr-watch.json' and appends one 'pr.changed' per changed pull request, whose subject is the
/// instance's 'fact:pr' exactly, and one 'idle' per quiet period (<see cref="IPrRemindAfter"/>). An unchanged pull request writes nothing
/// (<see cref="ScheduledResult.Quiet"/>). A host that fails backs off (doubling from a minute to 15), and is tried again only then; a refused call is
/// SND-FORGE-005, three unreachable polls SND-FORGE-006. Waiting instances are left as they are.
/// </summary>
/// <param name="watched">The pull requests being waited on.</param>
/// <param name="digests">The forge's digests.</param>
/// <param name="state">The last digests and the hosts' back-off.</param>
/// <param name="writer">Appends the event.</param>
/// <param name="remind">How long a quiet pull request waits before 'idle'.</param>
/// <param name="clock">Times the back-off and the quiet period.</param>
internal sealed class PrWatchService(IWatchedPulls watched, IPullDigests digests, IPrWatchState state, PrEventWriter writer, IPrRemindAfter remind, TimeProvider clock) : IScheduledService
{
    /// <summary>The service's name in the schedule ('pr-watch').</summary>
    public static readonly ServiceName ServiceId = ServiceName.From("pr-watch");

    /// <summary>The poller's place in the schedule without a profile entry: every 60 s.</summary>
    public static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromSeconds(60)));

    private const int UnreachableAfter = 3;

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var listed = await watched.List(cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var read = await state.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        if (listed.Value.Count == 0 && read.Value.Pulls.Count == 0 && read.Value.Hosts.Count == 0)
        {
            return ScheduledResult.Quiet;
        }

        var pass = new Pass(run, clock.GetUtcNow(), await remind.Get(cancellationToken).ConfigureAwait(false), read.Value);
        foreach (var group in listed.Value.GroupBy(static p => HostOf(p.Link), StringComparer.Ordinal))
        {
            if (group.Key is { } host)
            {
                await Poll(pass, host, [.. group], cancellationToken).ConfigureAwait(false);
            }
        }

        pass.Forget(listed.Value);
        var saved = await state.Write(pass.ToFile(), cancellationToken).ConfigureAwait(false);
        if (pass.Failure is { } failure)
        {
            return failure;
        }

        return saved.IsError ? saved.Error : pass.Appended > 0 ? ScheduledResult.Worked : ScheduledResult.Quiet;
    }

    private static string? HostOf(string link) => Uri.TryCreate(link, UriKind.Absolute, out var uri) ? uri.Host : null;

    /// <summary>Polls one host's pull requests in one digest call, unless the host is backing off.</summary>
    private async ValueTask Poll(Pass pass, string host, IReadOnlyList<WatchedPull> pulls, CancellationToken cancellationToken)
    {
        if (pass.Hosts.TryGetValue(host, out var backing) && backing.Until > pass.Now)
        {
            return;
        }

        var read = await Fetch([.. pulls.Select(static p => p.Link).Distinct(StringComparer.Ordinal)], cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            Fail(pass, host, backing, read.Error);
            return;
        }

        _ = pass.Hosts.Remove(host);
        var rows = new Dictionary<string, PullDigestRow>(StringComparer.Ordinal);
        foreach (var row in read.Value)
        {
            _ = rows.TryAdd(row.Link, row);
        }

        foreach (var pull in pulls)
        {
            if (rows.TryGetValue(pull.Link, out var row) && await Compare(pass, pull, row, cancellationToken).ConfigureAwait(false) is { } failure)
            {
                pass.Failure ??= failure;
                return;
            }
        }
    }

    /// <summary>Reads the digests; a refused call is read again once (the credential is read again, spec §10.2).</summary>
    private async ValueTask<Result<IReadOnlyList<PullDigestRow>>> Fetch(IReadOnlyList<string> links, CancellationToken cancellationToken)
    {
        var read = await digests.Digest(links, cancellationToken).ConfigureAwait(false);
        return read.IsError && string.Equals(read.Error.Code, SindriErrors.ForgeRefused.Code, StringComparison.Ordinal)
            ? await digests.Digest(links, cancellationToken).ConfigureAwait(false)
            : read;
    }

    /// <summary>A failed poll: the host backs off (a minute, doubling to 15), and the run ends in the error the spec names for it once it is one.</summary>
    private static void Fail(Pass pass, string host, PrWatchHost? backing, Error error)
    {
        var refused = string.Equals(error.Code, SindriErrors.ForgeRefused.Code, StringComparison.Ordinal);
        var unreachable = string.Equals(error.Code, SindriErrors.ForgeUnavailable.Code, StringComparison.Ordinal);
        if (!refused && !unreachable)
        {
            pass.Failure ??= error;
            return;
        }

        var failures = (backing?.Failures ?? 0) + 1;
        pass.Hosts[host] = new PrWatchHost(failures, pass.Now + TimeSpan.FromMinutes(Math.Min(15, 1 << Math.Min(failures - 1, 4))));
        if (refused)
        {
            pass.Failure ??= SindriErrors.ForgeCredentialRejected.With($"{host}: {error.Message}");
        }
        else if (failures >= UnreachableAfter)
        {
            pass.Failure ??= SindriErrors.ForgeUnreachable.With($"{host}: {error.Message}");
        }
    }

    /// <summary>Compares one pull request with the last poll: a change, or the quiet period running out, appends one event.</summary>
    private async ValueTask<Error?> Compare(Pass pass, WatchedPull pull, PullDigestRow row, CancellationToken cancellationToken)
    {
        _ = pass.Before.TryGetValue(pull.Link, out var before);
        var changes = PrChanges.Between(before, row);
        if (changes.Count > 0)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"pr-watch:{pull.Item}:{pull.Link}:{row.State}:{row.HeadSha}:{row.Digest}");
            return await Emit(pass, new PrChange(pull, changes[0], changes, row.HeadSha, key), new PrWatchEntry(row.Digest, row.HeadSha, row.State.ToString(), pass.Now), cancellationToken).ConfigureAwait(false);
        }

        if (before is null)
        {
            pass.Pulls[pull.Link] = new PrWatchEntry(row.Digest, row.HeadSha, row.State.ToString(), pass.Now);
            return null;
        }

        if (row.State == PullState.Open && pass.Now - before.QuietSince >= pass.RemindAfter)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"pr-watch:{pull.Item}:{pull.Link}:idle:{pass.Run.Slot:O}");
            return await Emit(pass, new PrChange(pull, "idle", [], row.HeadSha, key), before with { QuietSince = pass.Now }, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async ValueTask<Error?> Emit(Pass pass, PrChange change, PrWatchEntry seen, CancellationToken cancellationToken)
    {
        var appended = await writer.Append(pass.Run, change, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;   // the old entry stays, so the next poll finds the change again
        }

        pass.Pulls[change.Pull.Link] = seen;
        pass.Appended++;
        return null;
    }

    private sealed class Pass(ScheduledRun run, DateTimeOffset now, TimeSpan remindAfter, PrWatchFile file)
    {
        public ScheduledRun Run { get; } = run;

        public DateTimeOffset Now { get; } = now;

        public TimeSpan RemindAfter { get; } = remindAfter;

        /// <summary>What the last poll saw; fixed for the pass, so two instances on one pull request both see the change.</summary>
        public IReadOnlyDictionary<string, PrWatchEntry> Before { get; } = file.Pulls;

        public Dictionary<string, PrWatchEntry> Pulls { get; } = new(file.Pulls, StringComparer.Ordinal);

        public Dictionary<string, PrWatchHost> Hosts { get; } = new(file.Hosts, StringComparer.Ordinal);

        public int Appended { get; set; }

        public Error? Failure { get; set; }

        /// <summary>Drops what no instance waits on any more: the pull requests, and the hosts none of them is on.</summary>
        public void Forget(IReadOnlyList<WatchedPull> watched)
        {
            var links = watched.Select(static p => p.Link).ToHashSet(StringComparer.Ordinal);
            var hosts = watched.Select(static p => HostOf(p.Link)).OfType<string>().ToHashSet(StringComparer.Ordinal);
            foreach (var link in Pulls.Keys.Where(l => !links.Contains(l)).ToList())
            {
                _ = Pulls.Remove(link);
            }

            foreach (var host in Hosts.Keys.Where(h => !hosts.Contains(h)).ToList())
            {
                _ = Hosts.Remove(host);
            }
        }

        public PrWatchFile ToFile() => new(Pulls, Hosts);
    }
}
