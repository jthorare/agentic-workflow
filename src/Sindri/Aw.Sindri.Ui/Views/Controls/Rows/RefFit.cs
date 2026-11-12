using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>Cuts a ref in the middle so it fits a column, measured with the real mono font and size (#2978): at 130% text scale a fixed width per character is wrong.</summary>
internal static class RefFit
{
    /// <summary>The ref, cut in the middle until it fits.</summary>
    /// <param name="block">The text block that will show it; its resources give the font and size.</param>
    /// <param name="text">The full ref.</param>
    /// <param name="width">The column width.</param>
    /// <param name="fallbackCharWidth">The width of one character, used when the font can't be found.</param>
    /// <returns>The ref or its shortened form.</returns>
    public static string Fit(Control block, string text, double width, double fallbackCharWidth)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(text);
        var key = (text, width);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var fitted = Compute(block, text, width, fallbackCharWidth);
        if (_cache.Count > 4096)
        {
            _cache.Clear();
        }

        _cache[key] = fitted;
        return fitted;
    }

    private static readonly Dictionary<(string Text, double Width), string> _cache = [];

    private static string Compute(Control block, string text, double width, double fallbackCharWidth)
    {
        if (Measure(block, text) is not { } full)
        {
            return RefText.ShortenMiddle(text, Math.Max(4, (int)(width / fallbackCharWidth)));
        }

        if (full <= width)
        {
            return text;
        }

        var low = 4;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (Measure(block, RefText.ShortenMiddle(text, mid)) <= width)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return RefText.ShortenMiddle(text, low);
    }

    private static double? Measure(Control block, string text)
    {
        if (!block.TryFindResource("Font.Mono", block.ActualThemeVariant, out var family) || family is not FontFamily mono
            || !block.TryFindResource("Type.Caption.Size", block.ActualThemeVariant, out var size) || size is not double points)
        {
            return null;
        }

        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(mono), points, Brushes.Black).Width;
    }
}
