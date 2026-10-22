using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Which command a POST .../commands carries; only the kinds the app sends.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CommandKind>))]
internal enum CommandKind
{
    [JsonStringEnumMemberName("answer")]
    Answer = 1,

    [JsonStringEnumMemberName("cancel")]
    Cancel = 2,

    [JsonStringEnumMemberName("handback")]
    Handback = 3,
}
