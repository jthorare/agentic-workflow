using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Todos;

/// <summary>Where a converge takes a todo.</summary>
/// <param name="Side">What the todo should be.</param>
/// <param name="By">The 'by' of each event.</param>
/// <param name="DoneAt">The done time a 'todo.done' step keeps (an Undo passes the one the todo had before); null for the time of the event.</param>
public sealed record TodoConvergeTarget(TodoSide Side, string By, DateTimeOffset? DoneAt);
