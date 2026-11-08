using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Todos;

/// <summary>Which side of a both-places conflict a pick keeps (todo spec D11, §4.1): written in camel case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TodoSideName>))]
public enum TodoSideName
{
    /// <summary>Sindri's version of the todo.</summary>
    [JsonStringEnumMemberName("sindri")]
    Sindri,

    /// <summary>The Obsidian note's version of the todo.</summary>
    [JsonStringEnumMemberName("obsidian")]
    Obsidian,
}
