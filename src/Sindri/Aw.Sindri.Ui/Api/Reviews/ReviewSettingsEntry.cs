using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>A settings entry as written (and as PUT back): a key left null is not in the entry, so it comes from <c language="text">extends</c> or the default.</summary>
/// <param name="Extends">The entry this one builds on ("default" or another entry's name), or null.</param>
/// <param name="Panels">The shown panels in order (a left-out panel is hidden), or null.</param>
/// <param name="Groups">The diff groups, first match wins, or null.</param>
/// <param name="Collapse">Globs of files folded in the diff, or null.</param>
/// <param name="Risky">Paths that get a risk note, or null.</param>
/// <param name="Checks">The required checks, or null.</param>
internal sealed record ReviewSettingsEntry(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Extends = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Panels = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ReviewSettingsGroup>? Groups = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Collapse = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ReviewSettingsRisk>? Risky = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ReviewSettingsCheck>? Checks = null);
