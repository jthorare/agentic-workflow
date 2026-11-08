using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Todos;

/// <summary>What the CLI's id lookup needs of one todo.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Done">Whether it is checked off.</param>
/// <param name="Removed">Whether it is removed.</param>
public sealed record TodoCandidate(TodoId Id, bool Done, bool Removed);
