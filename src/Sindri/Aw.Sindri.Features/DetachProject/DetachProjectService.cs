using System.Globalization;
using Aw.Kernel.Services;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.DetachProject;

/// <summary>
/// Removes an attached or hand-made project (projects page spec §5.3; desk): reads it through the views, decides on the version the owner saw, and appends
/// 'project.detached' keyed by the project and the version it moves to. Losing the race for that key is SND-PROJECT-409.
/// </summary>
/// <param name="views">The attached projects.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class DetachProjectService(IViewReader views, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers)
    : IService<DetachProjectRequest, ProjectAccepted>
{
    public async ValueTask<Result<ProjectAccepted>> Handle(DetachProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var begun = ProjectWrite.Begin(callers, held, "DetachProject");
        if (begun.IsError)
        {
            return begun.Error;
        }

        var row = await views.Get<ProjectAttachedRow>(ViewNames.ProjectAttached, request.Project.Value, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        var (caller, grant) = begun.Value;
        var decided = ProjectDecisions.Detach(caller.CommandId, request.Project, row.Value, request.Version);
        if (decided.IsError)
        {
            return decided.Error;
        }

        var payload = decided.Value;
        var entry = events.Entry(EventTypes.ProjectDetached, payload, string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProjectDetached}:{request.Project.Value}:v{payload.Version}"));
        var appended = await ProjectWrite.Append(ledger, grant, entry, caller.CommandId, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ProjectAccepted(appended.Value, request.Project.Value, payload.Version, row.Value?.Name ?? row.Value?.Linear ?? request.Project.Value);
    }
}
