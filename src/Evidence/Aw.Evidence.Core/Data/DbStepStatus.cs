using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>How one db-run query ended.</summary>
[JsonConverter(typeof(StrictEnumConverter<DbStepStatus>))]
public enum DbStepStatus
{
    /// <summary>Its expectations held.</summary>
    [JsonStringEnumMemberName("passed")]
    Passed,

    /// <summary>An expectation did not hold.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>The database refused it (a write, a second statement, a bypass of read-only).</summary>
    [JsonStringEnumMemberName("refused")]
    Refused,

    /// <summary>It failed for another reason (no such table, a lost connection).</summary>
    [JsonStringEnumMemberName("broken")]
    Broken,

    /// <summary>A query after a refused or broken one.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,
}
