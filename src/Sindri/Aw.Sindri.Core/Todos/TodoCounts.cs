namespace Aw.Sindri.Core.Todos;

/// <summary>How many todos are in each state (todo spec §4.4).</summary>
/// <param name="Open">Open todos.</param>
/// <param name="Done">Done todos.</param>
/// <param name="Removed">Removed todos.</param>
public sealed record TodoCounts(int Open, int Done, int Removed);
