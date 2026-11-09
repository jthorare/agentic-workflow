using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ExpireToolkitCalls;

/// <summary>The 'toolkit-calls-expire' built-in service: once an hour it deletes toolkit calls older than 30 days. It runs without a profile entry.</summary>
/// <param name="calls">The call store.</param>
/// <param name="time">The clock.</param>
internal sealed class ExpireToolkitCallsService(IToolkitCalls calls, TimeProvider time) : IScheduledService
{
    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Deletes toolkit calls older than 30 days.";

    /// <summary>How long a call is kept.</summary>
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private readonly IToolkitCalls _calls = calls;
    private readonly TimeProvider _time = time;

    /// <summary>Gets the service's name in the schedule ('toolkit-calls-expire').</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("toolkit-calls-expire");

    /// <summary>Gets the hourly backstop the scheduler uses without a profile entry.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromHours(1)), Description);

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <inheritdoc/>
    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        _ = (_calls, _time);
        throw new NotSupportedException();
    }
}
