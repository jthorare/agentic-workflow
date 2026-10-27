using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Host.Server;

/// <summary>What the scheduler reads each tick besides the clock (API P6): the approved profile, the services the build runs without a profile entry, and the services a human paused; it writes the schedule it holds to the live schedule the services listing reads.</summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="builtIns">The built-in services.</param>
/// <param name="pauses">The paused services.</param>
/// <param name="live">Where the scheduler publishes its next slots.</param>
internal sealed class ScheduleInputs(IApprovals approvals, IEnumerable<BuiltInService> builtIns, IServicePauses pauses, LiveSchedule live)
{
    public IApprovals Approvals { get; } = approvals;

    public IReadOnlyList<BuiltInService> BuiltIns { get; } = [.. builtIns];

    public IServicePauses Pauses { get; } = pauses;

    public LiveSchedule Live { get; } = live;
}
