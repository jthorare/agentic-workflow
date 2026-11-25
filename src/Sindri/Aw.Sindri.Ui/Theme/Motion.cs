using Avalonia.Controls;

namespace Aw.Sindri.Ui.Theme;

/// <summary>
/// Reduced motion (UI spec §11.4, §14.3): the one place that sets the app's Motion.* durations. Reduced, every
/// duration is 0; otherwise each is its token's value from Theme/Tokens.axaml. The scaled copy is written over the
/// app's own dictionary, so DynamicResource users follow.
/// </summary>
internal static class Motion
{
    /// <summary>Sets every motion duration in <paramref name="resources"/> (the app's resources, with the tokens merged in).</summary>
    /// <param name="resources">The app's resources.</param>
    /// <param name="reduce">True for reduced motion (the OS setting or the in-app switch).</param>
    internal static void Apply(IResourceDictionary resources, bool reduce)
    {
        foreach (var key in _keys)
        {
            resources[key] = reduce ? TimeSpan.Zero : Base(resources, key);
        }
    }

    private static readonly string[] _keys = ["Motion.Instant", "Motion.Fast", "Motion.Hover", "Motion.Base", "Motion.Pulse", "Motion.Spin"];

    // The token's duration, from the merged token dictionary (the app's own dictionary holds the applied copy).
    private static TimeSpan Base(IResourceDictionary resources, string key) =>
        resources.MergedDictionaries.Select(d => d.TryGetResource(key, theme: null, out var value) ? value : null).OfType<TimeSpan>().First();
}
