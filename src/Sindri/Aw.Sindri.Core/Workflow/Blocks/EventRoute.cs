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
}
