namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The id pair the page needs to open the new session's pane.</summary>
/// <param name="Id">The row id.</param>
/// <param name="ActId">The key of the start; the worktree route keys on it.</param>
internal sealed record AgentStartedViewRef(string Id, string ActId);
