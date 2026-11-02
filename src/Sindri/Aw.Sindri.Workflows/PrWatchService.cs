using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Recipes;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The 'pr-watch' service (PR babysitter spec §6.2, API15): the producer of 'pr.changed'. It runs on the scheduler's shortest cadence and polls only every 'workflow.pr.pollInterval'
/// (default 60 s). A poll lists the pull requests of the live instances that wait on 'pr.changed', reads their digests through <see cref="IPullRequests.Digest"/> (the port batches
/// them at most 20 to a forge call), compares each with the last one in 'pr-watch.json', and records one 'pr.changed' per (item, pull request) that changed: its outcome the first of
/// merged, closed, updated, review and ci, its 'fact:prEvent' { subject, headSha, changes, at } with the subject the instance's 'fact:pr' exactly. A pull request seen for the first
/// time is only recorded. An unchanged one writes nothing, except one 'idle' per 'workflow.pr.remindAfter' of quiet. A run that wrote nothing is <see cref="ScheduledResult.Quiet"/>.
/// A host that doesn't answer is backed off by itself (the poll interval, doubling to 15 minutes), the other hosts are polled on. The run ends in the port's error when the service is
/// degraded: SND-FORGE-001 (no forge configured), SND-FORGE-002 (the credential or the host list is refused, after the port re-ran the credential command once) or, once three polls
/// in a row went unanswered, SND-FORGE-004; waiting instances are left as they are.
/// </summary>
/// <param name="watched">The pull requests the live instances wait on.</param>
/// <param name="pulls">The forge.</param>
/// <param name="recorder">Records the events.</param>
/// <param name="store">'pr-watch.json'.</param>
/// <param name="profile">The approved profile: the poll interval and the idle reminder.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The log.</param>
internal sealed partial class PrWatchService(IWatchedPulls watched, IPullRequests pulls, PrChangedRecorder recorder, PrWatchStore store, IApprovedProfile profile, TimeProvider clock, ILogger<PrWatchService> logger) : IScheduledService
{
    /// <summary>The longest a host is left alone after unanswered polls.</summary>
    internal static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(15);

    /// <summary>The poll interval when the profile sets none.</summary>
    internal static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(60);

    /// <summary>The quiet period before an 'idle' when the profile sets none.</summary>
    internal static readonly TimeSpan DefaultRemindAfter = TimeSpan.FromDays(14);

    /// <summary>The unanswered polls in a row after which the service reports SND-FORGE-004.</summary>
    internal const int UnreachablePolls = 3;

    /// <summary>The service's name in the schedule.</summary>
    public static readonly ServiceName ServiceId = ServiceName.From("pr-watch");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Watches the pull requests that wait on the forge.";

    /// <summary>The service's place in the schedule without a profile entry: the shortest cadence, with the poll interval kept by the service itself.</summary>
    public static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(Cadence.Min), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (await Settings(cancellationToken).ConfigureAwait(false) is not { } settings)
        {
            return ScheduledResult.Quiet;   // no approved profile names no forge and no pull request: nothing to watch
        }

        var read = await store.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var now = clock.GetUtcNow();
        if (read.Value.PolledAt is { } last && now - last < settings.Interval)
        {
            return ScheduledResult.Quiet;
        }

        var targets = await watched.List(cancellationToken).ConfigureAwait(false);
        if (targets.IsError)
        {
            return targets.Error;
        }

        var poll = new PrWatchPoll(run, now, settings, read.Value);
        await PollHosts(targets.Value, poll, cancellationToken).ConfigureAwait(false);
        return await Finish(targets.Value, read.Value, poll, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<WatchSettings?> Settings(CancellationToken cancellationToken)
    {
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is null)
        {
            return null;
        }

        var pr = approved.Value.Profile.Workflow?.Pr;
        return new WatchSettings(
            EffectSettings.Delay(pr?.PollInterval) ?? DefaultInterval,
            pr?.RemindAfter is { } text && RecipeDuration.TryFrom(text, out var duration) ? duration.Span : DefaultRemindAfter);
    }

    /// <summary>A pull request nobody waits on any more is forgotten; the file is rewritten while there is anything in it; a degraded poll ends in its error.</summary>
    private async ValueTask<Result<ScheduledResult>> Finish(IReadOnlyList<WatchedPull> targets, PrWatchFile before, PrWatchPoll poll, CancellationToken cancellationToken)
    {
        foreach (var link in poll.Pulls.Keys.Where(link => !targets.Any(t => string.Equals(t.Link, link, StringComparison.Ordinal))).ToList())
        {
            _ = poll.Pulls.Remove(link);
        }

        if (targets.Count > 0 || before.Pulls.Count > 0 || before.Hosts.Count > 0)
        {
            var written = await store.Write(new PrWatchFile(poll.Now, poll.Hosts, poll.Pulls), cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }
        }

        return poll.Failure is { } failure ? failure : poll.Recorded > 0 ? ScheduledResult.Worked : ScheduledResult.Quiet;
    }

    private async ValueTask PollHosts(IReadOnlyList<WatchedPull> targets, PrWatchPoll poll, CancellationToken cancellationToken)
    {
        foreach (var host in targets.GroupBy(static t => HostOf(t.Link), StringComparer.Ordinal))
        {
            if (poll.Hosts.TryGetValue(host.Key, out var backoff) && backoff.NextAt > poll.Now)
            {
                continue;
            }

            var links = host.Select(static t => t.Link).Distinct(StringComparer.Ordinal).ToList();
            var digests = await Digests(links, cancellationToken).ConfigureAwait(false);
            if (digests.IsError)
            {
                Unanswered(host.Key, digests.Error, poll);
                continue;
            }

            _ = poll.Hosts.Remove(host.Key);
            if (await CompareAll(host, digests.Value, poll, cancellationToken).ConfigureAwait(false) is { } failure)
            {
                poll.Failure ??= failure;
                return;
            }
        }
    }

    private async ValueTask<Error?> CompareAll(IEnumerable<WatchedPull> host, IReadOnlyList<KeyValuePair<string, PullDigest>> digests, PrWatchPoll poll, CancellationToken cancellationToken)
    {
        foreach (var (link, digest) in digests)
        {
            var items = host.Where(t => string.Equals(t.Link, link, StringComparison.Ordinal)).Select(static t => t.Item).Distinct(StringComparer.Ordinal).ToList();
            var done = await Compare(new PrSubject(link, items), digest, poll, cancellationToken).ConfigureAwait(false);
            if (done.IsError)
            {
                return done.Error;
            }
        }

        return null;
    }

    /// <summary>The digests by link. A pull request the forge no longer has fails the whole batch, so then each is read alone and the missing one is left out.</summary>
    private async ValueTask<Result<IReadOnlyList<KeyValuePair<string, PullDigest>>>> Digests(List<string> links, CancellationToken cancellationToken)
    {
        var read = await pulls.Digest(links, cancellationToken).ConfigureAwait(false);
        if (read.IsSuccess)
        {
            return read.Value.Count == links.Count
                ? Results.Success<IReadOnlyList<KeyValuePair<string, PullDigest>>>([.. links.Select((link, i) => KeyValuePair.Create(link, read.Value[i]))])
                : SindriErrors.ForgeUnavailable.With(string.Create(CultureInfo.InvariantCulture, $"the forge answered {read.Value.Count} digests for {links.Count} pull requests"));
        }

        return read.Error.Code != SindriErrors.ForgeNotFound.Code ? read.Error : await EachAlone(links, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<IReadOnlyList<KeyValuePair<string, PullDigest>>>> EachAlone(List<string> links, CancellationToken cancellationToken)
    {
        var found = new List<KeyValuePair<string, PullDigest>>();
        foreach (var link in links)
        {
            var one = await pulls.Digest([link], cancellationToken).ConfigureAwait(false);
            if (one.IsSuccess && one.Value.Count == 1)
            {
                found.Add(KeyValuePair.Create(link, one.Value[0]));
            }
            else if (one.IsError && one.Error.Code != SindriErrors.ForgeNotFound.Code)
            {
                return one.Error;
            }
        }

        return found;
    }

    private void Unanswered(string host, Error error, PrWatchPoll poll)
    {
        var failures = (poll.Hosts.TryGetValue(host, out var before) ? before.Failures : 0) + 1;
        var wait = TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, poll.Settings.Interval.Ticks * (1L << Math.Min(failures - 1, 20))));
        poll.Hosts[host] = new HostBackoff(failures, poll.Now + wait);
        Backoff(logger, host, error.Code, wait.TotalSeconds);
        // an unreachable or rate-limiting host is reported after the third poll in a row; a refused credential or a missing forge at once
        if (error.Code != SindriErrors.ForgeUnavailable.Code || failures >= UnreachablePolls)
        {
            poll.Failure ??= error;
        }
    }

    private async ValueTask<Result<Unit>> Compare(PrSubject subject, PullDigest digest, PrWatchPoll poll, CancellationToken cancellationToken)
    {
        var current = WatchedDigest.Of(digest);
        if (!poll.Pulls.TryGetValue(subject.Link, out var seen))
        {
            poll.Pulls[subject.Link] = new WatchedRecord(current, poll.Now, IdleAt: null);   // first sight: nothing to compare with
            return Unit.Value;
        }

        var changes = PrChanges.Between(seen.Digest, digest);
        var quietSince = seen.IdleAt is { } idle && idle > seen.ActiveAt ? idle : seen.ActiveAt;
        var change = changes.Count > 0
            ? new PrChange(subject, changes[0], changes, digest.HeadSha, $"{seen.Digest.Line()}->{current.Line()}")
            : poll.Now - quietSince >= poll.Settings.RemindAfter
                ? new PrChange(subject, "idle", [], digest.HeadSha, string.Create(CultureInfo.InvariantCulture, $"idle:{quietSince.UtcTicks}"))
                : null;
        if (change is null)
        {
            poll.Pulls[subject.Link] = seen with { Digest = current };
            return Unit.Value;
        }

        var recorded = await recorder.Record(change, poll, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        poll.Recorded += recorded.Value;
        poll.Pulls[subject.Link] = changes.Count > 0 ? new WatchedRecord(current, poll.Now, IdleAt: null) : seen with { Digest = current, IdleAt = poll.Now };
        return Unit.Value;
    }

    private static string HostOf(string link) => Uri.TryCreate(link, UriKind.Absolute, out var uri) ? uri.IdnHost.ToLowerInvariant() : "unknown";

    [LoggerMessage(Level = LogLevel.Warning, Message = "pr-watch: {Host} did not answer ({Code}); not asked for {Seconds} s")]
    private static partial void Backoff(ILogger logger, string host, string code, double seconds);
}
