using Aw.Kernel.Json;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Api;

/// <summary>preferences.json's bytes and <see cref="UiPreferences"/> (UI spec §12.3), through the app's JSON context.</summary>
internal static class PreferencesJson
{
    /// <summary>Parses a preferences file.</summary>
    /// <param name="json">The file's bytes, or null when there is none.</param>
    /// <returns>The clamped preferences, or <see cref="UiPreferences.Defaults"/> when there is no file, or it is malformed or of another version.</returns>
    internal static UiPreferences Read(ReadOnlySpan<byte> json)
    {
        var file = AwJson.Deserialize<PreferencesFile>(json, SindriApiClient.Json);
        if (file.IsError || file.Value.Version != UiPreferences.CurrentVersion)
        {
            return UiPreferences.Defaults;
        }

        var f = file.Value;
        var d = UiPreferences.Defaults;
        return new UiPreferences(
            f.Version,
            f.Window,
            f.SheetWidth ?? d.SheetWidth,
            f.LastPage ?? d.LastPage,
            f.SeenIds ?? [],
            f.PaletteHistory ?? [],
            f.TextScale ?? d.TextScale,
            f.ReduceMotion ?? d.ReduceMotion,
            d.SeenPages).Clamp();
    }

    /// <summary>The preferences as the file's bytes (UTF-8 JSON, camelCase), clamped first, so a file never holds a value out of range.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Write(UiPreferences preferences)
    {
        var p = preferences.Clamp();
        return AwJson.Serialize(new PreferencesFile(p.Version, p.Window, p.SheetWidth, p.LastPage, p.SeenIds, p.PaletteHistory, p.TextScale, p.ReduceMotion), SindriApiClient.Json);
    }
}
