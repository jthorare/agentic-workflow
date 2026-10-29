using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'project.attached' payload (projects page spec §5.1): the owner attached a Linear project to the page, or added one by hand.</summary>
/// <param name="CommandId">The command that attached it.</param>
/// <param name="Id">The project's id.</param>
/// <param name="Linear">The Linear project ('linear:project/&lt;id&gt;'), or null for a hand-made project.</param>
/// <param name="Name">The project's name; required when <paramref name="Linear"/> is null, null for a Linear project (Linear's name shows).</param>
/// <param name="State">The owner's state for it, or null (a Linear project then shows Linear's).</param>
/// <param name="Links">The owner's links: https urls without a query or fragment, at most 10.</param>
/// <param name="Note">The owner's one line, at most 200 characters, or null.</param>
/// <param name="Target">The target date, or null.</param>
public sealed record ProjectAttached(CommandId CommandId, ProjectId Id, string? Linear, string? Name, ProjectState? State, IReadOnlyList<string> Links, string? Note, DateOnly? Target);
