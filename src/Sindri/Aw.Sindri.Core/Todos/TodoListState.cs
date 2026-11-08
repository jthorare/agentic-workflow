namespace Aw.Sindri.Core.Todos;

/// <summary>Which todos a list shows (todo spec §4.4: 'GET /sindri/todos?state=open|done|all').</summary>
public enum TodoListState
{
    /// <summary>Open todos only (the default).</summary>
    Open,

    /// <summary>Done todos only.</summary>
    Done,

    /// <summary>Open, done and removed todos.</summary>
    All,
}
