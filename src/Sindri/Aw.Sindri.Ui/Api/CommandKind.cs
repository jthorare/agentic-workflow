using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api;

/// <summary>Which command a POST .../commands carries; only the kind the app still sends.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CommandKind>))]
internal enum CommandKind
{
    [JsonStringEnumMemberName("takeover")]
    Takeover = 5,
}
