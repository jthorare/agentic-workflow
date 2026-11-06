namespace Aw.Context.Testing.Linear;

/// <summary>A team, workflow state, member, label, cycle or project of the stateful fake Linear (only the columns that kind uses are set).</summary>
/// <param name="Id">The id.</param>
/// <param name="Name">The name.</param>
/// <param name="Key">A team's key ('ENG').</param>
/// <param name="Type">A workflow state's type (backlog, unstarted, started, completed, canceled).</param>
/// <param name="TeamId">The team a workflow state belongs to.</param>
internal sealed record LinearRow(string Id, string Name, string? Key = null, string? Type = null, string? TeamId = null);
