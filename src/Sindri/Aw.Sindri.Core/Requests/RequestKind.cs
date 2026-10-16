using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Requests;

/// <summary>What a session asks a human for (API spec §3.2 'FileRequest'); written in kebab case.</summary>
[JsonConverter(typeof(RequestKindConverter))]
public enum RequestKind
{
    /// <summary>Change the scope of the item.</summary>
    [JsonStringEnumMemberName("rescope")]
    Rescope,

    /// <summary>Add a task to the item.</summary>
    [JsonStringEnumMemberName("add-task")]
    AddTask,

    /// <summary>Tell the human something; nothing changes.</summary>
    [JsonStringEnumMemberName("note")]
    Note,

    /// <summary>Hand the work back to the human.</summary>
    [JsonStringEnumMemberName("handback")]
    Handback,
}
