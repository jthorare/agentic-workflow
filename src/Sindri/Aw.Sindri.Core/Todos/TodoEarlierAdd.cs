using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Todos;

/// <summary>One todo a client id made (todo spec §4.1).</summary>
/// <param name="Id">The todo.</param>
/// <param name="Sequence">The ledger sequence of its 'todo.added'.</param>
/// <param name="Due">Its due date when added.</param>
public sealed record TodoEarlierAdd(TodoId Id, long Sequence, DateOnly? Due);
