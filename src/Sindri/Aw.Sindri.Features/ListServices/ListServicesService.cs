using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListServices;

/// <summary>
/// The approved profile's services and the build's built-ins (name and cadence, plan G5), each joined with its row in the 'services' view: the
/// last run, and the next slot (the last slot plus the cadence, moved on by whole cadences past now; null before the first run). Paused and the running job come from P6.
/// A service the approval dropped leaves the list, though its row stays in the view.
/// </summary>
internal sealed class ListServicesService(IApprovedProfile approvals, IViewReader views, IProjectorStatus status, IJobViews jobs,
    IEnumerable<BuiltInService> builtIns, LiveSchedule live, TimeProvider clock)
    : IService<ListServicesRequest, ListServicesResponse>
{
    /// <summary>How far past its slot the scheduler's next slot may be before the service counts as overdue: the loop ticks every second, so a minute rules out a busy tick.</summary>
    internal static readonly TimeSpan OverdueGrace = TimeSpan.FromMinutes(1);

    public async ValueTask<Result<ListServicesResponse>> Handle(ListServicesRequest request, CancellationToken cancellationToken)
    {
        var current = status.Current;
        var asOf = current.AsOf;
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var rows = await views.List<ServiceRow>(ViewNames.Services, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var running = await jobs.Running(cancellationToken).ConfigureAwait(false);
        if (running.IsError)
        {
            return running.Error;
        }

        var named = approved.Value?.Profile.Services ?? SindriProfile.NoServices;
        var own = builtIns.Where(b => BuiltInService.Is(b.Name, named.ContainsKey(b.Name.Value), builtIns)).ToDictionary(b => b.Name.Value, b => b.Cadence.ToString(), StringComparer.Ordinal);   // a profile entry of the same name sets the cadence instead
        var runs = rows.Value.Where(r => named.ContainsKey(r.Name.Value) || own.ContainsKey(r.Name.Value)).ToDictionary(r => r.Name.Value, StringComparer.Ordinal);
        var described = Descriptions(builtIns);
        IReadOnlyList<ServiceView> services = [.. named.Select(s => (Name: s.Key, s.Value.Every)).Concat(own.Select(s => (Name: s.Key, Every: s.Value)))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => View(new Listed(s.Name, s.Every, described.GetValueOrDefault(s.Name), named.ContainsKey(s.Name)), runs.GetValueOrDefault(s.Name), running.Value.FirstOrDefault(j => j.Service?.Value == s.Name)?.Id))];
        return new ListServicesResponse(services, EmptyText.For(current, services.Count == 0, EmptyText.Services), asOf);
    }

    /// <summary>What each service the build knows does: the built-ins carry their own sentence, a profile service of the build (the profile has no field for one) is named in <see cref="ServiceDescriptions"/>.</summary>
    private static Dictionary<string, string> Descriptions(IEnumerable<BuiltInService> builtIns)
    {
        var described = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { ServiceDescriptions.HeartbeatName, ServiceDescriptions.JobProbeName })
        {
            if (ServiceDescriptions.Of(name) is { } text)
            {
                described[name] = text;
            }
        }

        foreach (var builtIn in builtIns)
        {
            if (builtIn.Description is { } text)
            {
                described[builtIn.Name.Value] = text;
            }
        }

        return described;
    }

    /// <summary>
    /// One service; the approved profile was checked, so its cadence parses (an unparseable one shows no next slot). The next slot is the scheduler's last
    /// slot plus the cadence, so a manual run does not move it, and there is none while the service is paused.
    /// </summary>
    private ServiceView View(Listed entry, ServiceRow? row, JobId? job)
    {
        var paused = row?.Paused == true;
        return new ServiceView(ServiceName.From(entry.Name), entry.Every, paused, row?.LastRun, paused ? null : NextSlot(entry.Every, row), job,
            ServiceActions.Allowed(entry.NamedByProfile, builtIn: !entry.NamedByProfile, paused, job is not null),
            entry.Description, paused ? row?.PausedBy : null, paused ? row?.PausedAt : null, OverdueSince(ServiceName.From(entry.Name), paused));
    }

    /// <summary>
    /// The scheduler's own next slot when it is more than <see cref="OverdueGrace"/> past: the loop moves a due slot on within a tick while it
    /// holds the lease (a paused service's slot passes unrun, a busy one is skipped), so a slot left behind was missed. Null while paused, before the
    /// scheduler loads a schedule, and for a service it does not hold.
    /// </summary>
    private DateTimeOffset? OverdueSince(ServiceName name, bool paused)
    {
        if (paused || live.Current is not { } slots || !slots.TryGetValue(name, out var slot))
        {
            return null;
        }

        return clock.GetUtcNow() - slot > OverdueGrace ? slot : null;
    }

    private DateTimeOffset? NextSlot(string every, ServiceRow? row)
    {
        var cadence = Cadence.Parse(every);
        return row?.ScheduledSlot is { } slot && cadence.IsSuccess ? Upcoming(slot, cadence.Value.Interval) : null;
    }

    /// <summary>One service of the list before its run is joined: the name, the cadence text, the sentence, and whether the profile names it.</summary>
    private sealed record Listed(string Name, string Every, string? Description, bool NamedByProfile);

    /// <summary>
    /// The first slot of the series after <paramref name="slot"/> that is still to come. An idle run after an ok one records nothing (C8), so the
    /// recorded slot ages while the scheduler ticks on; the series it advances (ServiceSchedule.Advance) is slot plus whole cadences.
    /// </summary>
    private DateTimeOffset Upcoming(DateTimeOffset slot, TimeSpan interval)
    {
        var next = slot + interval;
        var now = clock.GetUtcNow();
        return next > now ? next : next + (interval * (long)(Math.Floor((now - next) / interval) + 1));
    }
}
