using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Core.Projects;

/// <summary>A row of the 'project-attached' view: one attached or hand-made project, keyed by its id. A removed project's row is deleted, so its Linear key can be attached again.</summary>
/// <param name="Id">The project.</param>
/// <param name="Linear">The Linear project ('linear:project/&lt;id&gt;'), or null for a hand-made project.</param>
/// <param name="Name">The owner's name for a hand-made project, or null.</param>
/// <param name="State">The owner's state, or null.</param>
/// <param name="Links">The owner's links.</param>
/// <param name="Notes">The owner's free-form notes, or null.</param>
/// <param name="Target">The target date, or null.</param>
/// <param name="Version">1 when attached, one more per change.</param>
/// <param name="AttachedAt">When it was attached.</param>
/// <param name="FinishedAt">When the event that set Done or Canceled was recorded; null while the state is anything else.</param>
/// <param name="EditedAt">When the owner last changed it (a hand-made project's last activity).</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
public sealed record ProjectAttachedRow(
    ProjectId Id,
    string? Linear,
    string? Name,
    ProjectState? State,
    IReadOnlyList<string> Links,
    string? Notes,
    DateOnly? Target,
    long Version,
    DateTimeOffset AttachedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset EditedAt,
    long Sequence);
