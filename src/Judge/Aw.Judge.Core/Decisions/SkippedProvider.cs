using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Decisions;

/// <summary>A provider the chain passed over, and why.</summary>
/// <param name="Provider">The provider name, e.g. 'claude-cli'.</param>
/// <param name="Reason">Why it didn't decide.</param>
public sealed record SkippedProvider(string Provider, SkipReason Reason)
{
    /// <summary>
    /// The finer reason the provider gave, when it gave one ('no-reasons', 'images-off', 'image-out-of-limits', 'image-too-large');
    /// null for a skip that <see cref="Reason"/> says all of, so a row without one stores exactly what it stored before.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }
}
