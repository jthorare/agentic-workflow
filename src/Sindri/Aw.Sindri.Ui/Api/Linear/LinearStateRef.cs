namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The workflow state of an issue.</summary>
/// <param name="Id">The state's Linear id.</param>
/// <param name="Name">The team's name for it.</param>
/// <param name="Type">backlog, unstarted, started, completed, canceled or triage.</param>
internal sealed record LinearStateRef(string Id, string Name, string Type);
