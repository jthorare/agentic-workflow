namespace Aw.Sindri.Features.LinearIssues;

/// <summary>An issue's workflow state. The name is team-defined Linear text: fenced for a session.</summary>
/// <param name="Id">The state's Linear id.</param>
/// <param name="Name">The state's name, 'In Review'.</param>
/// <param name="Type">backlog, unstarted, started, completed, canceled or triage.</param>
internal sealed record LinearStateView(string Id, string Name, string Type);
