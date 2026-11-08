namespace Aw.Sindri.Ui.Api.Todos;

/// <summary>GET /sindri/todos?state=open|done|all (todo spec §4.4): the todos and the counts. A view read.</summary>
/// <param name="AsOf">How fresh the view is.</param>
/// <param name="State">The state filter that was served.</param>
/// <param name="Counts">Open, done and removed totals.</param>
/// <param name="Todos">The todos, as the server orders them.</param>
internal sealed record TodoListView(AsOfView AsOf, string State, TodoCountsView Counts, IReadOnlyList<TodoView> Todos);
