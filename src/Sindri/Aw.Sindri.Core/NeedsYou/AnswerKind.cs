using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>The kind of answer a status layout offers (WF section 6.4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnswerKind>))]
public enum AnswerKind
{
    /// <summary>A text answer.</summary>
    [JsonStringEnumMemberName("text")]
    Text = 1,

    /// <summary>A bare resume.</summary>
    [JsonStringEnumMemberName("resume")]
    Resume = 2,

    /// <summary>Grant rounds.</summary>
    [JsonStringEnumMemberName("grant")]
    Grant = 3,

    /// <summary>Grant tokens.</summary>
    [JsonStringEnumMemberName("grantTokens")]
    GrantTokens = 4,

    /// <summary>Accept at a terminal.</summary>
    [JsonStringEnumMemberName("accept")]
    Accept = 5,

    /// <summary>Only cancel ends it.</summary>
    [JsonStringEnumMemberName("cancel-only")]
    CancelOnly = 6,
}
