namespace Aw.Context.Infrastructure.Linear;

/// <summary>A project's status: the team-renamable name and the fixed type (backlog, planned, started, paused, completed or canceled).</summary>
/// <param name="Name">The Name Linear serves.</param>
/// <param name="Type">The Type Linear serves.</param>
internal sealed record LinearProjectStatus(string Name, string Type);
