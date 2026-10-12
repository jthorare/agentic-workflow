using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>How the app's start form asks for a recipe input (spec §3.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InputType>))]
public enum InputType
{
    /// <summary>Typed text.</summary>
    [JsonStringEnumMemberName("text")]
    Text = 1,

    /// <summary>A file's contents.</summary>
    [JsonStringEnumMemberName("file")]
    File = 2,

    /// <summary>An item.</summary>
    [JsonStringEnumMemberName("item")]
    Item = 3,
}
