namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>Shortens a reference in the middle, so its end (the part that tells refs apart) stays.</summary>
internal static class RefText
{
    private const char Ellipsis = '…';
    private const int MinHead = 4;

    /// <summary>
    /// Shortens <paramref name="text"/> to at most <paramref name="maxChars"/> characters. A ref with a slash keeps its last segment and as much of the start as is left
    /// (acme-platform/exports reads acme-…/exports); when that segment is too long, or there is no slash, the middle goes and both ends stay.
    /// </summary>
    /// <param name="text">The reference, such as acme-platform/exports.</param>
    /// <param name="maxChars">The most characters to return.</param>
    /// <returns>The shortened text, or <paramref name="text"/> when it fits.</returns>
    public static string ShortenMiddle(string text, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= maxChars)
        {
            return text;
        }

        if (maxChars <= 1)
        {
            return maxChars == 1 ? Ellipsis.ToString() : string.Empty;
        }

        var slash = text.LastIndexOf('/');
        if (slash > 0)
        {
            var tail = text[slash..];
            var head = maxChars - 1 - tail.Length;
            if (head >= MinHead)
            {
                return text[..head] + Ellipsis + tail;
            }
        }

        var budget = maxChars - 1;
        var end = budget / 2;
        return text[..(budget - end)] + Ellipsis + text[^end..];
    }
}
