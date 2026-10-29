using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Projects;

/// <summary>
/// Folds the five project events into the 'project-attached' and 'project-hidden' views (projects page spec §5.2): a row per attached or hand-made project and a row per
/// led Linear project the owner ever hid or wrote notes on (one row, one version counter for both). A removal deletes the row (so the Linear key can be attached again, and a rebuild never has to carry tombstones); a change or
/// removal for a row the view doesn't hold is skipped.
/// </summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class ProjectsProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.ProjectAttached, ViewNames.ProjectHidden];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.ProjectAttached, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ProjectChanged, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ProjectDetached, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ProjectHidden, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ProjectLedNoted, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        return recorded.Envelope.Type switch
        {
            EventTypes.ProjectAttached => await Attached(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.ProjectChanged => await Changed(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.ProjectDetached => await Detached(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.ProjectLedNoted => await LedNoted(recorded, rows, cancellationToken).ConfigureAwait(false),
            _ => await Hidden(recorded, rows, cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>The row a 'project.attached' makes: version 1, finished when it starts out Done or Canceled.</summary>
    /// <param name="attached">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ProjectAttachedRow Fold(ProjectAttached attached, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(attached);
        ArgumentNullException.ThrowIfNull(recorded);
        var at = recorded.Envelope.OccurredAt;
        return new ProjectAttachedRow(
            attached.Id, attached.Linear, attached.Name, attached.State, attached.Links, attached.Notes ?? attached.Note, attached.Target,
            Version: 1, at, IsFinished(attached.State) ? at : default(DateTimeOffset?), at, recorded.Sequence);
    }

    /// <summary>The row after a 'project.changed': the asked fields set, the cleared ones dropped, the version moved, and the finish time set when the state newly became Done or Canceled and dropped when it left them.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="changed">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row.</returns>
    public static ProjectAttachedRow Fold(ProjectAttachedRow row, ProjectChanged changed, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(recorded);
        var at = recorded.Envelope.OccurredAt;
        var state = changed.ClearState ? null : changed.State ?? row.State;
        return row with
        {
            Name = changed.Name ?? row.Name,
            State = state,
            Links = changed.Links ?? row.Links,
            Notes = NotesAfter(row, changed),
            Target = changed.ClearTarget ? null : changed.Target ?? row.Target,
            Version = changed.Version,
            FinishedAt = FinishedAt(row, state, at),
            EditedAt = at,
            Sequence = recorded.Sequence,
        };
    }

    // events written before notes was renamed carry 'note' and 'clearNote' (owner q158): read them as the same field
    private static string? NotesAfter(ProjectAttachedRow row, ProjectChanged changed) =>
        changed.ClearNotes || changed.ClearNote ? null : changed.Notes ?? changed.Note ?? row.Notes;

    // finished_at is when the event that set Done or Canceled was recorded: kept while the state stays, moved when it changes between the two, null once it leaves them
    private static DateTimeOffset? FinishedAt(ProjectAttachedRow row, ProjectState? state, DateTimeOffset at) =>
        !IsFinished(state) ? null : state == row.State ? row.FinishedAt ?? at : at;

    private static bool IsFinished(ProjectState? state) => state is ProjectState.Done or ProjectState.Canceled;

    private async ValueTask<Result<Unit>> Attached(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var attached = events.Payload<ProjectAttached>(recorded);
        return attached.IsError
            ? attached.Error
            : await rows.Put(ViewNames.ProjectAttached, attached.Value.Id.Value, Fold(attached.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Changed(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var changed = events.Payload<ProjectChanged>(recorded);
        if (changed.IsError)
        {
            return changed.Error;
        }

        var row = await rows.Get<ProjectAttachedRow>(ViewNames.ProjectAttached, changed.Value.Id.Value, cancellationToken).ConfigureAwait(false);
        return row.IsError
            ? row.Error
            : row.Value is null
                ? Unit.Value
                : await rows.Put(ViewNames.ProjectAttached, changed.Value.Id.Value, Fold(row.Value, changed.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Detached(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var detached = events.Payload<ProjectDetached>(recorded);
        return detached.IsError
            ? detached.Error
            : await rows.Delete(ViewNames.ProjectAttached, detached.Value.Id.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Hidden(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var hidden = events.Payload<ProjectHidden>(recorded);
        if (hidden.IsError)
        {
            return hidden.Error;
        }

        // one row holds both the hide state and the notes of a led card, so a hide keeps the notes the row already has
        var row = await rows.Get<ProjectHiddenRow>(ViewNames.ProjectHidden, hidden.Value.Linear, cancellationToken).ConfigureAwait(false);
        return row.IsError
            ? row.Error
            : await rows.Put(ViewNames.ProjectHidden, hidden.Value.Linear, new ProjectHiddenRow(hidden.Value.Linear, hidden.Value.Hidden, hidden.Value.Version, recorded.Sequence, row.Value?.Notes), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> LedNoted(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var noted = events.Payload<ProjectLedNoted>(recorded);
        if (noted.IsError)
        {
            return noted.Error;
        }

        // ... and a notes save keeps the hide state (a card never hidden is a row with hidden = false)
        var row = await rows.Get<ProjectHiddenRow>(ViewNames.ProjectHidden, noted.Value.Linear, cancellationToken).ConfigureAwait(false);
        return row.IsError
            ? row.Error
            : await rows.Put(ViewNames.ProjectHidden, noted.Value.Linear, new ProjectHiddenRow(noted.Value.Linear, row.Value?.Hidden ?? false, noted.Value.Version, recorded.Sequence, noted.Value.Notes), cancellationToken).ConfigureAwait(false);
    }
}
