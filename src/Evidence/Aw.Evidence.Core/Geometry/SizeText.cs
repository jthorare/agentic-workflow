using System.Globalization;

namespace Aw.Evidence.Core.Geometry;

/// <summary>Reads a size written as 'WxH' ('1440x900').</summary>
public static class SizeText
{
    private const int Largest = 8192;

    /// <summary>Parses 'WxH' with both sides whole numbers from 1 to 8192.</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The size, when the text is one.</param>
    /// <returns>True when <paramref name="text"/> is a size.</returns>
    public static bool TryParse(string? text, out ViewportSize size)
    {
        size = new ViewportSize(0, 0);
        var parts = text?.Split('x');
        if (parts is not [var width, var height]
            || !int.TryParse(width, NumberStyles.None, CultureInfo.InvariantCulture, out var w)
            || !int.TryParse(height, NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            || w is < 1 or > Largest || h is < 1 or > Largest)
        {
            return false;
        }

        size = new ViewportSize(w, h);
        return true;
    }
}
