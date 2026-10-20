using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListServices;

/// <summary>
/// The approved profile's services and the build's built-ins (name and cadence, plan G5), each joined with its row in the 'services' view: the
/// last run, and the next slot (the last slot plus the cadence; null before the first run). Paused and the running job come from P6.
/// A service the approval dropped leaves the list, though its row stays in the view.
/// </summary>
internal sealed class ListServicesService(IApprovedProfile approvals, IViewReader views, IProjectorStatus status, IJobViews jobs,
    IEnumerable<BuiltInService> builtIns)
    : IService<ListServicesRequest, ListServicesResponse>
{
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
        var own = builtIns.Where(b => !named.ContainsKey(b.Name.Value)).ToDictionary(b => b.Name.Value, b => b.Cadence.ToString(), StringComparer.Ordinal);   // a profile entry of the same name sets the cadence instead
        var runs = rows.Value.Where(r => named.ContainsKey(r.Name.Value) || own.ContainsKey(r.Name.Value)).ToDictionary(r => r.Name.Value, StringComparer.Ordinal);
        IReadOnlyList<ServiceView> services = [.. named.Select(s => (Name: s.Key, s.Value.Every)).Concat(own.Select(s => (Name: s.Key, Every: s.Value)))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => View(s.Name, s.Every, runs.GetValueOrDefault(s.Name), running.Value.FirstOrDefault(j => j.Service?.Value == s.Name)?.Id))];
        return new ListServicesResponse(services, EmptyText.For(current, services.Count == 0, EmptyText.Services), asOf);
    }

    /// <summary>
    /// One service; the approved profile was checked, so its cadence parses (an unparseable one shows no next slot). The next slot is the scheduler's last
    /// slot plus the cadence, so a manual run does not move it, and there is none while the service is paused.
    /// </summary>
    private static ServiceView View(string name, string every, ServiceRow? row, JobId? job)
    {
        var cadence = Cadence.Parse(every);
        var paused = row?.Paused == true;
        DateTimeOffset? next = !paused && row?.ScheduledSlot is { } slot && cadence.IsSuccess ? slot + cadence.Value.Interval : null;
        return new ServiceView(ServiceName.From(name), every, paused, row?.LastRun, next, job);
    }
}
