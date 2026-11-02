using System.Text.Json.Serialization;

namespace Aw.Context.Core.Records;

/// <summary>Where a source's sync stands (spec §5.5); internal, the owner sees only usable and complete.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SyncPhase>))]
public enum SyncPhase
{
    /// <summary>The recent window, newest first.</summary>
    [JsonStringEnumMemberName("recent")]
    Recent,

    /// <summary>Older records back to the window's start, one page per tick.</summary>
    [JsonStringEnumMemberName("backfill")]
    Backfill,

    /// <summary>Changes since the cursor.</summary>
    [JsonStringEnumMemberName("incremental")]
    Incremental,

    /// <summary>A bounded full window that catches deletes and missed edits.</summary>
    [JsonStringEnumMemberName("reconcile")]
    Reconcile,
}
