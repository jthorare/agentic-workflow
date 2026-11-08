namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>The counts of GET /sindri/todos (todo spec §4.4): how many todos sit in each state, whatever the list's state filter.</summary>
/// <param name="Open">Open todos.</param>
/// <param name="Done">Done todos.</param>
/// <param name="Removed">Removed todos.</param>
internal sealed record TodoCountsView(int Open, int Done, int Removed);
