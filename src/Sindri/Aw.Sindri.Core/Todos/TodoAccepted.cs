using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Todos;

/// <summary>What a todo write answers (todo spec §4.4): the accepted command, the todo, the version it is at and the due date the write read.</summary>
/// <param name="Accepted">The accepted command.</param>
/// <param name="Id">The todo's id.</param>
/// <param name="Version">The version the todo is at now.</param>
/// <param name="Due">The due date it has now, or null.</param>
public sealed record TodoAccepted(CommandAccepted Accepted, TodoId Id, long Version, DateOnly? Due);
