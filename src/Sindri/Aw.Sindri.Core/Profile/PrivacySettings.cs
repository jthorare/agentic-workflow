using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// What never leaves the box in a self-evolution proposal (issue 3787; evolve privacy spec 2026-10-09 §5.2, §5.3). Used only when a proposal's public version is
/// gated, never by the scrubber.
/// </summary>
/// <param name="DenyTerms">Names that never go public: a term holds in every spelling of its parts ('AcmeCare' holds 'acme-care').</param>
/// <param name="DenyPatterns">.NET regexes matched case-insensitively on the visible text; each must compile.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrivacySettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? DenyTerms = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? DenyPatterns = null);
