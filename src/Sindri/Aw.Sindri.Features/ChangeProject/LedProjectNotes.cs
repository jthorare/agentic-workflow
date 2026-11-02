using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ChangeProject;

/// <summary>
/// Writes the notes of a Linear project the owner leads (owner q158), behind <see cref="ChangeProjectService"/>: checks through the project facts that the owner leads it (SND-PROJECT-404 when not), reads its row
/// (the hide row, which holds the card's version and notes), decides on the version the owner saw, scrubs the notes, and appends 'project.led.noted' keyed by the Linear key and
/// the version it moves to. Losing the race for that key is SND-PROJECT-409.
/// </summary>
/// <param name="views">The hide rows.</param>
/// <param name="facts">The stored Linear projects.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="scrubber">Redacts the notes.</param>
internal sealed class LedProjectNotes(IViewReader views, IProjectFacts facts, ILedgerStore ledger, EventFactory events, ITextScrubber scrubber)
{
    /// <summary>Writes the notes.</summary>
    /// <param name="caller">The caller, already checked as the owner.</param>
    /// <param name="grant">The lease grant for the append.</param>
    /// <param name="request">The led project's Linear key, the card version the owner saw and the edit.</param>
    /// <param name="cancellationToken">Cancels the reads and the append.</param>
    /// <returns>The accepted write, SND-PROJECT-404 for a project the owner does not lead, or the decision's refusal.</returns>
    public async ValueTask<Result<ProjectAccepted>> Write(CallerContext caller, LeaseGrant grant, ChangeProjectRequest request, CancellationToken cancellationToken)
    {
        var led = await Leads(request.Project, cancellationToken).ConfigureAwait(false);
        if (led.IsError)
        {
            return led.Error;
        }

        var row = await views.Get<ProjectHiddenRow>(ViewNames.ProjectHidden, request.Project, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        var decided = ProjectDecisions.LedNotes(caller.CommandId, request.Project, row.Value, request.Edit, request.Version);
        if (decided.IsError)
        {
            return decided.Error;
        }

        var payload = decided.Value with { Notes = ProjectText.Notes(decided.Value.Notes, scrubber) };
        var entry = events.Entry(EventTypes.ProjectLedNoted, payload, string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProjectLedNoted}:{payload.Linear}:v{payload.Version}"));
        var appended = await ProjectWrite.Append(ledger, grant, entry, caller.CommandId, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ProjectAccepted(appended.Value, payload.Linear, payload.Version, payload.Linear);
    }

    // a key the owner doesn't lead (or the store doesn't hold) is the same not-found as an unknown prj_ id
    private async ValueTask<Result<Unit>> Leads(string linear, CancellationToken cancellationToken)
    {
        var read = await facts.Read([], cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return read.Value.Catalog.Any(fact => fact.LeadIsOwner && string.Equals(fact.Key, linear, StringComparison.Ordinal))
            ? Unit.Value
            : SindriErrors.ProjectNotFound.With($"No project {linear}. `sindri projects` lists them.");
    }
}
