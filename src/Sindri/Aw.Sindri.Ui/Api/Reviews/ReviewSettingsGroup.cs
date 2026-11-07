using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One diff group of a settings entry (spec §7.1, with §16's <c language="text">folded</c>).</summary>
/// <param name="Name">The group's heading.</param>
/// <param name="Globs">The paths it takes.</param>
/// <param name="Folded">True when the group shows collapsed ("Lockfiles, collapsed, 14 files"); null is false.</param>
internal sealed record ReviewSettingsGroup(
    string Name,
    IReadOnlyList<string> Globs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Folded = null);
