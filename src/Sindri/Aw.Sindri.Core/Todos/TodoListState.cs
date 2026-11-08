using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Todos;

/// <summary>Which todos a list shows (todo spec §4.4: 'GET /sindri/todos?state=open|done|all').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TodoListState>))]
public enum TodoListState
{
    /// <summary>Open todos only (the default).</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>Done todos only.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>Open, done and removed todos.</summary>
    [JsonStringEnumMemberName("all")]
    All,
}
