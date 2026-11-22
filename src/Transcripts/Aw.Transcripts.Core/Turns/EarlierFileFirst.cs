namespace Aw.Transcripts.Core.Turns;

/// <summary>
/// Orders files for <see cref="TurnDedupe"/>: by first timestamp, then last (a resumed session starts with copies of the
/// original's lines but runs longer), then path. File names are random, so they don't say which file is the original.
/// A file with no timestamp sorts last.
/// </summary>
public sealed class EarlierFileFirst : IComparer<FileSpan>
{
    /// <summary>The one instance.</summary>
    public static EarlierFileFirst Instance { get; } = new();

    /// <inheritdoc />
    public int Compare(FileSpan? x, FileSpan? y) => (x, y) switch
    {
        (null, null) => 0,
        (null, _) => -1,
        (_, null) => 1,
        _ => CompareSpans(x, y),
    };

    private static int CompareSpans(FileSpan x, FileSpan y)
    {
        var first = Key(x.First).CompareTo(Key(y.First));
        if (first != 0)
        {
            return first;
        }

        var last = Key(x.Last).CompareTo(Key(y.Last));
        return last != 0 ? last : string.CompareOrdinal(x.Identity, y.Identity);
    }

    private static DateTimeOffset Key(DateTimeOffset? at) => at ?? DateTimeOffset.MaxValue;
}
