namespace Aw.Context.Testing.Linear;

/// <summary>A team, workflow state, member, label, cycle or project of the stateful fake Linear (only the columns that kind uses are set).</summary>
/// <param name="Id">The id.</param>
/// <param name="Name">The name.</param>
/// <param name="Key">A team's key ('ENG').</param>
/// <param name="Type">A workflow state's type (backlog, unstarted, started, completed, canceled).</param>
/// <param name="TeamId">The team a workflow state, label or cycle belongs to (null for a workspace label or a cycle every team has).</param>
/// <param name="StartsAt">A cycle's start (ISO 8601).</param>
/// <param name="EndsAt">A cycle's end (ISO 8601).</param>
internal sealed record LinearRow(string Id, string Name, string? Key = null, string? Type = null, string? TeamId = null, string? StartsAt = null, string? EndsAt = null);
