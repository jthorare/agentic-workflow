using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Messages;

/// <summary>The rail's lists (messages spec §6.2, GET /sindri/messages/lists/{list}).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MessageList>))]
public enum MessageList
{
    /// <summary>Threads that wait on my answer (§7.1, the one definition).</summary>
    [JsonStringEnumMemberName("needs-reply")]
    NeedsReply,

    /// <summary>Conversations with lines past the local read pointer.</summary>
    [JsonStringEnumMemberName("unread")]
    Unread,

    /// <summary>Threads with the Later flag (Release 2).</summary>
    [JsonStringEnumMemberName("later")]
    Later,

    /// <summary>Unsent drafts (Release 2).</summary>
    [JsonStringEnumMemberName("drafts")]
    Drafts,
}
