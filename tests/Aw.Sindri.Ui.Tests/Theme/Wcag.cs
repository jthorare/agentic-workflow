using System.Globalization;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>WCAG 2.2 relative luminance and contrast ratio for #rrggbb colours.</summary>
internal static class Wcag
{
    internal static double Luminance(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 8)
        {
            h = h[2..];   // #aarrggbb: the tokens the contrast table names are opaque
        }

        double Channel(int at)
        {
            var c = int.Parse(h.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(0)) + (0.7152 * Channel(2)) + (0.0722 * Channel(4));
    }

    internal static double Contrast(string a, string b)
    {
        a = Over(a, b);
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    // A translucent #aarrggbb (a hairline: white at 6 %) is drawn over the background it sits on; the ratio is of the blended colour.
    private static string Over(string a, string b)
    {
        var f = a.TrimStart('#');
        if (f.Length != 8)
        {
            return a;
        }

        var back = b.TrimStart('#');
        var alpha = int.Parse(f.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var blended = string.Concat(Enumerable.Range(0, 3).Select(i =>
        {
            var fg = int.Parse(f.AsSpan(2 + (i * 2), 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var bg = int.Parse(back.AsSpan(back.Length - 6 + (i * 2), 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ((int)Math.Round((fg * alpha) + (bg * (1 - alpha)), MidpointRounding.AwayFromZero)).ToString("x2", CultureInfo.InvariantCulture);
        }));
        return "#" + blended;
    }
}
