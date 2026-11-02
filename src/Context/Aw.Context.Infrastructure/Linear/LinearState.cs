namespace Aw.Context.Infrastructure.Linear;

/// <summary>An issue's workflow state: Linear's own word and its type (triage, backlog, unstarted, started, completed or canceled).</summary>
/// <param name="Name">The Name Linear serves.</param>
/// <param name="Type">The Type Linear serves.</param>
internal sealed record LinearState(string Name, string Type);
