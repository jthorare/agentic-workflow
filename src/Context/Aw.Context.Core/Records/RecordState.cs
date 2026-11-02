using System.Text.Json.Serialization;

namespace Aw.Context.Core.Records;

/// <summary>A tracker record's normalised state (spec §4.2); the source's own word is kept beside it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecordState>))]
public enum RecordState
{
    /// <summary>Open.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>Closed.</summary>
    [JsonStringEnumMemberName("closed")]
    Closed,

    /// <summary>A pull request that was merged.</summary>
    [JsonStringEnumMemberName("merged")]
    Merged,

    /// <summary>A draft.</summary>
    [JsonStringEnumMemberName("draft")]
    Draft,

    /// <summary>Canceled.</summary>
    [JsonStringEnumMemberName("canceled")]
    Canceled,

    /// <summary>Done.</summary>
    [JsonStringEnumMemberName("done")]
    Done,
}
