using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Questions;

/// <summary>Who sends a message through wake-gate; its JSON text is 'teammate', 'subagent', 'main' or 'background' (hook contract).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SenderKind>))]
public enum SenderKind
{
    /// <summary>A teammate agent.</summary>
    [JsonStringEnumMemberName("teammate")]
    Teammate = 1,

    /// <summary>A subagent.</summary>
    [JsonStringEnumMemberName("subagent")]
    Subagent = 2,

    /// <summary>The main session.</summary>
    [JsonStringEnumMemberName("main")]
    Main = 3,

    /// <summary>A background task.</summary>
    [JsonStringEnumMemberName("background")]
    Background = 4,
}
