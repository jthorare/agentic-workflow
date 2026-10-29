using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.AttachProject;

/// <summary>
/// Attaches a Linear project or adds a hand-made one (projects page spec §5.3; desk): reads the attached projects through the views and the stored
/// Linear projects through the project facts (a typed url or key must be one of them; one the owner leads is already on the page), decides
/// through <see cref="ProjectDecisions"/>, scrubs the notes and links, and appends 'project.attached' keyed by the new project id.
/// </summary>
/// <param name="lookup">The attached projects and the stored Linear project a typed url or key names.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="scrubber">Redacts the notes and links.</param>
/// <param name="callers">The caller: the command id.</param>
/// <param name="ids">The id source for the project.</param>
internal sealed class AttachProjectService(AttachProjectLookup lookup, ILedgerStore ledger, HeldLease held, EventFactory events, ITextScrubber scrubber, ICallerContextAccessor callers, IIdGenerator ids)
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

        var found = await lookup.Read(linear.Value, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var (caller, grant) = begun.Value;
        var id = ProjectId.New(ids);
        var (key, led, attached, ledNotes) = found.Value;
        var draft = new ProjectDraft(key, request.Name, request.State, request.Links, request.Notes ?? ledNotes, request.Target);
        var decided = ProjectDecisions.Attach(caller.CommandId, id, draft, attached, led);
        if (decided.IsError)
        {
            return decided.Error;
        }

        var payload = decided.Value with { Notes = ProjectText.Notes(decided.Value.Notes, scrubber), Links = ProjectText.Links(decided.Value.Links, scrubber) ?? [] };
        var entry = events.Entry(EventTypes.ProjectAttached, payload, $"{EventTypes.ProjectAttached}:{id.Value}");
        var appended = await ProjectWrite.Append(ledger, grant, entry, caller.CommandId, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ProjectAccepted(appended.Value, id.Value, Version: 1, payload.Name ?? payload.Linear ?? id.Value);
    }

    /// <summary>What a typed Linear project names; null when none was typed.</summary>
    private static Result<LinearProjectRef?> Linear(string? typed)
    {
        if (typed is null)
        {
            return Results.Success<LinearProjectRef?>(value: null);
        }

        var parsed = LinearProjectRefs.Parse(typed);
        return parsed.IsError ? parsed.Error : parsed.Value;
    }
}
