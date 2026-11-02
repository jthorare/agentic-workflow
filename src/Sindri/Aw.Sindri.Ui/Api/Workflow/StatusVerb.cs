using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A status command's verb.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StatusVerb>))]
internal enum StatusVerb
{
    [JsonStringEnumMemberName("answer")]
    Answer = 1,

    [JsonStringEnumMemberName("complete")]
    Complete = 2,

    [JsonStringEnumMemberName("handback")]
    Handback = 3,

    [JsonStringEnumMemberName("takeover")]
    Takeover = 4,

    [JsonStringEnumMemberName("cancel")]
    Cancel = 5,

    [JsonStringEnumMemberName("approve")]
    Approve = 6,

    [JsonStringEnumMemberName("rewind")]
    Rewind = 7,
}
