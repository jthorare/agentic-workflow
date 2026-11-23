using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Messages;

/// <summary>What a Slack conversation is (messages spec §8.1, the conversations table's kind).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConversationKind>))]
public enum ConversationKind
{
    /// <summary>A public channel.</summary>
    [JsonStringEnumMemberName("channel")]
    Channel,

    /// <summary>A private channel.</summary>
    [JsonStringEnumMemberName("private")]
    Private,

    /// <summary>A direct message.</summary>
    [JsonStringEnumMemberName("dm")]
    Dm,

    /// <summary>A group DM.</summary>
    [JsonStringEnumMemberName("mpim")]
    Mpim,
}
