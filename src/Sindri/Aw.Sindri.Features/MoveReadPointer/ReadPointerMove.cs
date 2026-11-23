using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.MoveReadPointer;

/// <summary>Which way a read pointer moves.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReadPointerMove>))]
public enum ReadPointerMove
{
    /// <summary>Mark read up to the ts.</summary>
    [JsonStringEnumMemberName("read")]
    Read,

    /// <summary>Mark unread from the ts.</summary>
    [JsonStringEnumMemberName("unread")]
    Unread,
}
