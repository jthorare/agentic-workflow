using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ChangeProject;

/// <summary>
/// Changes an attached or hand-made project (projects page spec §5.3; desk): reads it through the views, decides on the version the owner saw, scrubs
/// the notes and links, and appends 'project.changed' keyed by the project and the version it moves to. Losing the race for that key is SND-PROJECT-409.
/// </summary>
/// <param name="views">The attached projects.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="scrubber">Redacts the notes and links.</param>
/// <param name="callers">The caller: the command id.</param>
/// <param name="ledNotes">Writes the notes of a project the owner leads, when the request names one by its Linear key.</param>
internal sealed class ChangeProjectService(IViewReader views, ILedgerStore ledger, HeldLease held, EventFactory events, ITextScrubber scrubber, ICallerContextAccessor callers, LedProjectNotes ledNotes)
    : IService<ChangeProjectRequest, ProjectAccepted>
{
    public async ValueTask<Result<ProjectAccepted>> Handle(ChangeProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var begun = ProjectWrite.Begin(callers, held, "ChangeProject");
        if (begun.IsError)
        {
            return begun.Error;
        }

        var (caller, grant) = begun.Value;
        return ProjectDecisions.NamesLinearProject(request.Project)
            ? await ledNotes.Write(caller, grant, request, cancellationToken).ConfigureAwait(false)
            : await Change(caller, grant, request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ProjectAccepted>> Change(CallerContext caller, LeaseGrant grant, ChangeProjectRequest request, CancellationToken cancellationToken)
    {
        if (!ProjectId.TryFrom(request.Project, out var id))
        {
            return SindriErrors.InvalidInput.With($"'{SafeText.Line(SafeText.Escape(request.Project), 80)}' isn't a project id (prj_…) or a Linear project key");
        }

        var row = await views.Get<ProjectAttachedRow>(ViewNames.ProjectAttached, id.Value, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        var decided = ProjectDecisions.Change(caller.CommandId, id, row.Value, request.Edit, request.Version);
        if (decided.IsError)
        {
            return decided.Error;
        }

        var payload = decided.Value with { Notes = ProjectText.Notes(decided.Value.Notes, scrubber), Links = ProjectText.Links(decided.Value.Links, scrubber) };
        var entry = events.Entry(EventTypes.ProjectChanged, payload, string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProjectChanged}:{id.Value}:v{payload.Version}"));
        var appended = await ProjectWrite.Append(ledger, grant, entry, caller.CommandId, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ProjectAccepted(appended.Value, id.Value, payload.Version, payload.Name ?? row.Value?.Name ?? row.Value?.Linear ?? id.Value);
    }
}
