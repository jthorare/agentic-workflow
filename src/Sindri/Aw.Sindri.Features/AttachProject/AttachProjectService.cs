using Aw.Kernel;
using Aw.Kernel.Ids;
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

namespace Aw.Sindri.Features.AttachProject;

/// <summary>
/// Attaches a Linear project or adds a hand-made one (projects page spec §5.3; desk): reads the attached projects through the views, decides
/// through <see cref="ProjectDecisions"/>, scrubs the note and links, and appends 'project.attached' keyed by the new project id.
/// </summary>
/// <param name="views">The attached projects.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="scrubber">Redacts the note and links.</param>
/// <param name="callers">The caller: the command id.</param>
/// <param name="ids">The id source for the project.</param>
internal sealed class AttachProjectService(IViewReader views, ILedgerStore ledger, HeldLease held, EventFactory events, ITextScrubber scrubber, ICallerContextAccessor callers, IIdGenerator ids)
    : IService<AttachProjectRequest, ProjectAccepted>
{
    public async ValueTask<Result<ProjectAccepted>> Handle(AttachProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var begun = ProjectWrite.Begin(callers, held, "AttachProject");
        if (begun.IsError)
        {
            return begun.Error;
        }

        var linear = Linear(request.Linear);
        if (linear.IsError)
        {
            return linear.Error;
        }

        var attached = await views.List<ProjectAttachedRow>(ViewNames.ProjectAttached, cancellationToken).ConfigureAwait(false);
        if (attached.IsError)
        {
            return attached.Error;
        }

        var (caller, grant) = begun.Value;
        var id = ProjectId.New(ids);
        var draft = new ProjectDraft(linear.Value, request.Name, request.State, request.Links, request.Note, request.Target);
        var decided = ProjectDecisions.Attach(caller.CommandId, id, draft, attached.Value, led: false);   // Ph4: led comes from Context's project feed
        if (decided.IsError)
        {
            return decided.Error;
        }

        var payload = decided.Value with { Note = ProjectText.Note(decided.Value.Note, scrubber), Links = ProjectText.Links(decided.Value.Links, scrubber) ?? [] };
        var entry = events.Entry(EventTypes.ProjectAttached, payload, $"{EventTypes.ProjectAttached}:{id.Value}");
        var appended = await ProjectWrite.Append(ledger, grant, entry, caller.CommandId, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ProjectAccepted(appended.Value, id.Value, Version: 1, payload.Name ?? payload.Linear ?? id.Value);
    }

    /// <summary>The key a typed Linear project names; null when none was typed.</summary>
    private static Result<string?> Linear(string? typed)
    {
        if (typed is null)
        {
            return (string?)null;
        }

        var parsed = LinearProjectRefs.Parse(typed);
        return parsed.IsError ? parsed.Error
            : parsed.Value.Key is { } key ? key
            // Ph4: look the url's slug id up among the stored Linear projects (SND-PROJECT-003 when none matches); Context has no facade member that answers that yet
            : SindriErrors.ProjectInvalid.With("linear: a project url can't be looked up yet (pass the key: linear:project/<id>)");
    }
}
