using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The variants of an answer the app sends: a hint, or a refusal that stops the run. A grant of rounds or tokens is one click: the app sends the amount and no challenge answer (q93). Accept is the terminal's.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnswerVariant>))]
internal enum AnswerVariant
{
    [JsonStringEnumMemberName("text")]
    Text = 1,

    [JsonStringEnumMemberName("reject")]
    Reject = 2,

    [JsonStringEnumMemberName("grant-tokens")]
    GrantTokens = 3,

    [JsonStringEnumMemberName("grant")]
    Grant = 4,
}
