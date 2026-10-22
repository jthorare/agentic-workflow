using Aw.Kernel;
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

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) => throw new NotSupportedException();
}
