using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Host.Server;

/// <summary>What the scheduler reads each tick besides the clock (API P6): the profile, the services the build runs without a profile entry, and the services a human paused; it writes the schedule it holds to the live schedule the services listing reads.</summary>
/// <param name="approvals">The profile.</param>
/// <param name="builtIns">The built-in services.</param>
/// <param name="pauses">The paused services.</param>
/// <param name="live">Where the scheduler publishes its next slots.</param>
/// <param name="pollWake">Asked for when the machine woke, so the poll service catches up; none in rigs that do not poll.</param>
internal sealed class ScheduleInputs(IApprovedProfile approvals, IEnumerable<BuiltInService> builtIns, IServicePauses pauses, LiveSchedule live, PollWake? pollWake = null)
{
    public IApprovedProfile Approvals { get; } = approvals;

    public IReadOnlyList<BuiltInService> BuiltIns { get; } = [.. builtIns];

    public IServicePauses Pauses { get; } = pauses;

    public LiveSchedule Live { get; } = live;

    public PollWake? PollWake { get; } = pollWake;
}
