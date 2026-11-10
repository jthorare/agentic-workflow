using Avalonia.Controls;

namespace Aw.Sindri.Ui.Theme;

/// <summary>
/// The user's text size (UI spec §14.2, Settings): scales the type ramp in Theme/Tokens.axaml by 0.9–1.3. The base sizes
/// stay in the token dictionary; the scaled ones are written over them, so the typography styles' DynamicResource
/// setters follow.
/// </summary>
internal static class TextScale
{
    /// <summary>The smallest scale.</summary>
    internal const double Min = 0.9;

    /// <summary>The largest scale.</summary>
    internal const double Max = 1.3;

    /// <summary>Scales the ramp in <paramref name="resources"/> (the app's resources, with the tokens merged in).</summary>
    /// <param name="resources">The app's resources.</param>
    /// <param name="scale">The scale; clamped to <see cref="Min"/>–<see cref="Max"/>.</param>
    internal static void Apply(IResourceDictionary resources, double scale)
    {
        var clamped = double.IsFinite(scale) ? Math.Clamp(scale, Min, Max) : 1.0;
        foreach (var step in _steps)
        {
            foreach (var key in (string[])[$"Type.{step}.Size", $"Type.{step}.LineHeight"])
            {
                resources[key] = Base(resources, key) * clamped;
            }
        }

        foreach (var key in _sizes)
        {
            resources[key] = Base(resources, key) * clamped;
        }

        resources["Type.Scale"] = clamped;
    }

    // Sizes that follow the text: the icon box (Theme/Icons.axaml), so a glyph stays as tall as its word, the "."
    // keycap's dot and a keycap's narrowest width.
    private static readonly string[] _sizes = ["Size.Icon", "Size.KeyDot", "Size.KeyCap"];

    private static readonly string[] _steps = ["PageTitle", "SectionTitle", "DetailTitle", "Body", "BodyStrong", "Caption", "Eyebrow", "Button", "Mono", "Kbd"];

    // The unscaled value, from the merged token dictionary (the app's own dictionary holds the scaled copy).
    private static double Base(IResourceDictionary resources, string key) =>
        resources.MergedDictionaries.Select(d => d.TryGetResource(key, theme: null, out var value) ? value : null).OfType<double>().First();
}
