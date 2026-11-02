using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'project.changed' payload (projects page spec §5.1): the owner edited an attached or hand-made project. A field that is null is left
/// as it was; <see cref="Links"/>, when set, replaces the whole list; a field the owner emptied is named by its 'Clear' flag
/// (<see cref="ClearState"/>, <see cref="ClearNotes"/>, <see cref="ClearTarget"/>), so "unchanged" and "cleared" never share a value.
/// </summary>
/// <param name="CommandId">The command that changed it.</param>
/// <param name="Id">The project's id.</param>
/// <param name="Version">Its version after the change (the fact key's 'v&lt;version&gt;').</param>
/// <param name="Name">A new name (hand-made projects only), or null.</param>
/// <param name="State">A new state, or null.</param>
/// <param name="ClearState">Whether the owner's state is dropped (a Linear project then shows Linear's again).</param>
/// <param name="Links">The new whole list of links, or null.</param>
/// <param name="Target">A new target date, or null.</param>
/// <param name="ClearTarget">Whether the target date is dropped.</param>
/// <param name="Notes">New notes, or null.</param>
/// <param name="ClearNotes">Whether the notes are dropped.</param>
/// <param name="Note">Read only: the one-line note older events carry (renamed to notes, owner q158); never written.</param>
/// <param name="ClearNote">Read only: older events' flag for dropping the note; never written.</param>
public sealed record ProjectChanged(
    CommandId CommandId,
    ProjectId Id,
    long Version,
    string? Name,
    ProjectState? State,
    bool ClearState,
    IReadOnlyList<string>? Links,
    DateOnly? Target,
    bool ClearTarget,
    string? Notes = null,
    bool ClearNotes = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ClearNote = false);
