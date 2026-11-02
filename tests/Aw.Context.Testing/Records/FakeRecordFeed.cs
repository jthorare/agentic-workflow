using System.Globalization;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>The in-memory <see cref="RecordFeed"/>: a fixed list of items paged by position (the cursor is the next index) after the window's start is applied; it passes <see cref="RecordFeedContract{TFeed}"/>.</summary>
/// <param name="source">The source it reads.</param>
/// <param name="items">Every item, in the order the feed serves them.</param>
public sealed class FakeRecordFeed(SourceId source, IReadOnlyList<RecordItem> items) : RecordFeed(source)
{
    /// <inheritdoc />
    public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        var start = pull.After is { } cursor ? int.Parse(cursor.Value, CultureInfo.InvariantCulture) : 0;
        var inWindow = items.Where(i => (pull.NotBefore is not { } notBefore || i.UpdatedAt is not { } updated || updated >= notBefore) && (pull.Before is not { } before || i.UpdatedAt is not { } changed || changed < before)).ToList();
        var page = inWindow.Skip(start).Take(pull.Limit).ToList();
        var next = SourceCursor.From((start + page.Count).ToString(CultureInfo.InvariantCulture));
        return ValueTask.FromResult<Result<RecordPage>>(new RecordPage(page, [], next, CursorReset: false));
    }
}
