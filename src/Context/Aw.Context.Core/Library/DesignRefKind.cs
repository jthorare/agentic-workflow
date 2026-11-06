using System.Text.Json.Serialization;

namespace Aw.Context.Core.Library;

/// <summary>How a design-doc ref links its two ends.</summary>
[JsonConverter(typeof(StrictEnumConverter<DesignRefKind>))]
public enum DesignRefKind
{
    /// <summary>A test-plan row verifies the item.</summary>
    [JsonStringEnumMemberName("verifies")]
    Verifies,

    /// <summary>The two items relate (a task's <c language="text">reqs:</c>, a traceability row).</summary>
    [JsonStringEnumMemberName("relates")]
    Relates,

    /// <summary>The item depends on the target (a task's <c language="text">depends:</c>).</summary>
    [JsonStringEnumMemberName("blocks")]
    Blocks,
}
