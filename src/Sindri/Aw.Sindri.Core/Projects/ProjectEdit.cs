using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Core.Projects;

/// <summary>What the owner asked to change on a project (projects page spec §5.3): a null field is left as it was, a 'Clear' flag drops the field.</summary>
/// <param name="Name">A new name (hand-made projects only), or null.</param>
/// <param name="State">A new state, or null.</param>
/// <param name="ClearState">Drops the owner's state.</param>
/// <param name="Links">The new whole list of links, or null.</param>
/// <param name="Note">A new note, or null.</param>
/// <param name="ClearNote">Drops the note.</param>
/// <param name="Target">A new target date, or null.</param>
/// <param name="ClearTarget">Drops the target date.</param>
public sealed record ProjectEdit(
    string? Name = null,
    ProjectState? State = null,
    bool ClearState = false,
    IReadOnlyList<string>? Links = null,
    string? Note = null,
    bool ClearNote = false,
    DateOnly? Target = null,
    bool ClearTarget = false);
