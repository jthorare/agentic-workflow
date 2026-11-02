using System.Globalization;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// The Linear feed's cursor: 's=' the watermark a finished run committed (the next run asks for issues updated after it), 'a=' Linear's own end cursor inside a run that is still paging newest first,
/// 't=' the newest updatedAt that run has seen, which becomes the watermark when it reaches the end. A run cut short therefore repeats work and loses nothing.
/// </summary>
/// <param name="Since">The committed watermark, or null.</param>
/// <param name="After">Linear's end cursor of the last page, or null at the start of a run.</param>
/// <param name="Top">The newest updatedAt seen in this run, or null.</param>
internal sealed record FeedCursor(DateTimeOffset? Since, string? After, DateTimeOffset? Top)
{
    /// <summary>The start: no watermark, no page, nothing seen.</summary>
    public static FeedCursor Empty { get; } = new(Since: null, After: null, Top: null);

    /// <summary>Reads a cursor; null or one this feed did not write starts over.</summary>
    /// <param name="cursor">The persisted cursor.</param>
    /// <returns>The parts.</returns>
    public static FeedCursor Read(SourceCursor? cursor)
    {
        if (cursor is not { } c)
        {
            return Empty;
        }

        DateTimeOffset? since = null, top = null;
        string? after = null;
        foreach (var part in c.Value.Split(';'))
        {
            if (part.Length < 2 || part[1] != '=')
            {
                return Empty;
            }

            var value = part[2..];
            switch (part[0])
            {
                case 's':
                    since = Time(value);
                    break;
                case 'a':
                    after = value.Length == 0 ? null : value;
                    break;
                case 't':
                    top = Time(value);
                    break;
                default:
                    return Empty;
            }
        }

        return new FeedCursor(since, after, top);
    }

    /// <summary>The cursor as persisted.</summary>
    /// <returns>The cursor.</returns>
    public SourceCursor ToCursor() => SourceCursor.From("s=" + Text(Since) + ";a=" + After + ";t=" + Text(Top));

    private static string Text(DateTimeOffset? at) => at?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

    private static DateTimeOffset? Time(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : null;
}
