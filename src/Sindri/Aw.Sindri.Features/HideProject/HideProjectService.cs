using System.Globalization;
using Aw.Kernel.Services;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.HideProject;

/// <summary>
/// Hides or unhides a Linear project the owner leads (projects page spec §5.3; desk): reads its hide row through the views (none before the first hide),
/// decides on the hide version the owner saw, and appends 'project.hidden' keyed by the Linear key and the version it moves to. Losing the race for that key is SND-PROJECT-409.
/// Whether the owner really leads the project is not checked yet (Ph4, with Context's project feed).
/// </summary>
/// <param name="views">The hide rows.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class HideProjectService(IViewReader views, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers)
    : IService<HideProjectRequest, ProjectAccepted>
{
    public async ValueTask<Result<ProjectAccepted>> Handle(HideProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var begun = ProjectWrite.Begin(callers, held, "HideProject");
        if (begun.IsError)
        {
            return begun.Error;
        }

        var row = await views.Get<ProjectHiddenRow>(ViewNames.ProjectHidden, request.Linear, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        var (caller, grant) = begun.Value;
        var decided = ProjectDecisions.Hide(caller.CommandId, request.Linear, request.Hidden, row.Value, request.Version);
        if (decided.IsError)
        {
            return decided.Error;
        }

        var payload = decided.Value;
        var entry = events.Entry(EventTypes.ProjectHidden, payload, string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProjectHidden}:{payload.Linear}:v{payload.Version}"));
        var appended = await ProjectWrite.Append(ledger, grant, entry, caller.CommandId, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ProjectAccepted(appended.Value, payload.Linear, payload.Version, payload.Linear);
    }
}
