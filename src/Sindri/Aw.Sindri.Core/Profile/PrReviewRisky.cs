using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>Paths that deserve a second look.</summary>
/// <param name="Globs">The paths.</param>
/// <param name="Why">The reason shown beside them.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrReviewRisky(IReadOnlyList<string> Globs, string Why);
