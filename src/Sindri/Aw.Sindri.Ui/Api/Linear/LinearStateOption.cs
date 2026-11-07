namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>A workflow state a team offers.</summary>
/// <param name="Id">The state's Linear id.</param>
/// <param name="Name">The team's name for it.</param>
/// <param name="Type">backlog, unstarted, started, completed, canceled or triage.</param>
/// <param name="Position">Its place in the team's board, from 0.</param>
internal sealed record LinearStateOption(string Id, string Name, string Type, int Position);
