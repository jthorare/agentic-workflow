using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>One named settings entry (spec §7.1). Every key left out is taken from 'extends' (else from 'default'); a list set here replaces the inherited list.</summary>
/// <param name="Extends">'default' or another entry's name; no cycles.</param>
/// <param name="Panels">The panels shown, in order; each at most once, and leaving one out hides it.</param>
/// <param name="Groups">File groups; the first match wins and unmatched files go to "Other".</param>
/// <param name="Collapse">Globs of files the diff folds by default.</param>
/// <param name="Risky">Paths a reviewer should look at twice, each with the reason.</param>
/// <param name="Checks">The required checks (§7.3).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrReviewSettingsEntry(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Extends = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Panels = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PrReviewGroup>? Groups = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Collapse = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PrReviewRisky>? Risky = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PrReviewCheck>? Checks = null);
