using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One lens's report in a round.</summary>
/// <param name="Lens">The lens.</param>
/// <param name="Model">The model it ran on.</param>
/// <param name="NotApplicable">Why it did not apply, when it did not.</param>
/// <param name="Findings">What it found.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopLensRow(
    string Lens,
    string Model,
    string? NotApplicable,
    IReadOnlyList<StopFinding> Findings);
