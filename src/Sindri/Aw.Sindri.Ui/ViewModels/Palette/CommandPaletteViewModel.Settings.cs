namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>The command palette (<see cref="CommandPaletteViewModel"/>, continued): its Settings entries (#3362).</summary>
internal sealed partial class CommandPaletteViewModel
{
    // The Settings section's entries (#3362): the setting's id, its name and the words that filter the Settings page to it.
    private static readonly (string Id, string Name, string Words)[] _settings =
    [
        ("TextSize", "Text size", "text size"), ("ReduceMotion", "Reduce motion", "reduce motion"), ("Setup", "Setup checklist", "checklist"),
        ("ModelChoice", "Model choice", "model"), ("Sandbox", "Sandbox", "sandbox"), ("Library", "Library", "library"),
        ("Linear", "Linear connection", "linear"), ("ObsidianNote", "Obsidian todo note", "obsidian"),
    ];

    private static PaletteEntry[] SettingEntries() => [.. _settings.Select(static s => PaletteEntry.ForSetting(s.Id, s.Name, s.Words))];
}
