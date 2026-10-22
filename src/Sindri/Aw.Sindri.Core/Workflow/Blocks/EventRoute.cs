using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>How an event reaches an instance (spec §2, §5.7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EventRoute>))]
public enum EventRoute
{
    /// <summary>Only to the frame of the command it answers.</summary>
    [JsonStringEnumMemberName("causation")]
    Causation = 1,

    /// <summary>To the item's waiting instances.</summary>
    [JsonStringEnumMemberName("item")]
    Item = 2,

    /// <summary>To the instance linked as the parent of the instance whose command caused the event, or to the causer itself when it is not linked (line spec LN-WF1).</summary>
    [JsonStringEnumMemberName("parent")]
    Parent = 3,
}
