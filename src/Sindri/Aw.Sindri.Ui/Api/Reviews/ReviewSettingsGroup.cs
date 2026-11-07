namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One diff group of a settings entry (spec §7.1): a heading and the paths it takes. Folding is the entry's <c language="text">collapse</c> globs, not a group flag.</summary>
/// <param name="Name">The group's heading.</param>
/// <param name="Globs">The paths it takes.</param>
internal sealed record ReviewSettingsGroup(string Name, IReadOnlyList<string> Globs);
