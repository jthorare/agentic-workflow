using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'project.changed' payload (projects page spec §5.1): the owner edited an attached or hand-made project. A field that is null is left
/// as it was; <see cref="Links"/>, when set, replaces the whole list; a field the owner emptied is named by its 'Clear' flag
/// (<see cref="ClearState"/>, <see cref="ClearNote"/>, <see cref="ClearTarget"/>), so "unchanged" and "cleared" never share a value.
/// </summary>
/// <param name="CommandId">The command that changed it.</param>
/// <param name="Id">The project's id.</param>
/// <param name="Version">Its version after the change (the fact key's 'v&lt;version&gt;').</param>
/// <param name="Name">A new name (hand-made projects only), or null.</param>
/// <param name="State">A new state, or null.</param>
/// <param name="ClearState">Whether the owner's state is dropped (a Linear project then shows Linear's again).</param>
/// <param name="Links">The new whole list of links, or null.</param>
/// <param name="Note">A new note, or null.</param>
/// <param name="ClearNote">Whether the note is dropped.</param>
/// <param name="Target">A new target date, or null.</param>
/// <param name="ClearTarget">Whether the target date is dropped.</param>
public sealed record ProjectChanged(
    CommandId CommandId,
    ProjectId Id,
    long Version,
    string? Name,
    ProjectState? State,
    bool ClearState,
    IReadOnlyList<string>? Links,
    string? Note,
    bool ClearNote,
    DateOnly? Target,
    bool ClearTarget);
