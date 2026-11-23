using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Messages;

/// <summary>A thread's class (messages spec §7.2): fixed rules set it, never a model (decision 15). High first.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MessageClass>))]
public enum MessageClass
{
    /// <summary>Not a mine thread with a new message from someone else: no class.</summary>
    [JsonStringEnumMemberName("none")]
    None = 0,

    /// <summary>Class 1, FYI: any other mine thread with a new message from someone else, a keyword hit, a mention with no question.</summary>
    [JsonStringEnumMemberName("fyi")]
    Fyi = 1,

    /// <summary>Class 2, review ask: the thread links a PR or issue the row store says waits on me.</summary>
    [JsonStringEnumMemberName("reviewAsk")]
    ReviewAsk = 2,

    /// <summary>Class 3, direct question: the newest message is another person's, in a small DM or naming me, and holds '?' or a request phrase.</summary>
    [JsonStringEnumMemberName("directQuestion")]
    DirectQuestion = 3,
}
