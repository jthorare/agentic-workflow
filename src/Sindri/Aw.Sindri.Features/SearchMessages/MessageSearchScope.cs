using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.SearchMessages;

/// <summary>Where a search looks (messages spec §9.8).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MessageSearchScope>))]
public enum MessageSearchScope
{
    /// <summary>The local store's FTS index (Release 1).</summary>
    [JsonStringEnumMemberName("local")]
    Local,

    /// <summary>Slack's search.messages as a second pass (Release 3).</summary>
    [JsonStringEnumMemberName("slack")]
    Slack,

    /// <summary>Messages and every other Sindri source (Release 3).</summary>
    [JsonStringEnumMemberName("everything")]
    Everything,
}
