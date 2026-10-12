using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A block parameter's type (spec §2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ParameterType>))]
public enum ParameterType
{
    /// <summary>Text.</summary>
    [JsonStringEnumMemberName("text")]
    Text = 1,

    /// <summary>A number.</summary>
    [JsonStringEnumMemberName("number")]
    Number = 2,

    /// <summary>True or false.</summary>
    [JsonStringEnumMemberName("flag")]
    Flag = 3,

    /// <summary>A list of strings, numbers or bools.</summary>
    [JsonStringEnumMemberName("list")]
    List = 4,
}
