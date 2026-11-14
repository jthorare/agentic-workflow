namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>Maps a row's kind and state class to the glyph it is drawn with.</summary>
internal static class RowGlyphs
{
    // One glyph per state class, in the enum's order: Open, Active, Waiting, Blocked, Done, Failed.
    private static readonly RowGlyph[] _pr = [RowGlyph.PrOpen, RowGlyph.PrOpen, RowGlyph.InReview, RowGlyph.NeedsYou, RowGlyph.Merged, RowGlyph.Failed];
    private static readonly RowGlyph[] _run = [RowGlyph.Todo, RowGlyph.AgentRunning, RowGlyph.NeedsYou, RowGlyph.Parked, RowGlyph.Done, RowGlyph.Failed];
    private static readonly RowGlyph[] _thread = [RowGlyph.Message, RowGlyph.Message, RowGlyph.Message, RowGlyph.Message, RowGlyph.Done, RowGlyph.Failed];
    private static readonly RowGlyph[] _work = [RowGlyph.Todo, RowGlyph.Doing, RowGlyph.NeedsYou, RowGlyph.Parked, RowGlyph.Done, RowGlyph.Failed];

    /// <summary>Picks the glyph for a row.</summary>
    /// <param name="kind">The row's kind: pr, issue, run, thread, session, item, decision, note.</param>
    /// <param name="state">The row's state class.</param>
    /// <param name="stateKey">The API's state key, for a glyph the class alone cannot pick (draft, in review, CI running); null when not needed.</param>
    /// <returns>The glyph.</returns>
    public static RowGlyph For(string kind, RowStateClass state, string? stateKey = null)
    {
        ArgumentNullException.ThrowIfNull(kind);
        var lower = kind.ToLowerInvariant();
        if (ByKey(lower == "pr", stateKey) is { } keyed)
        {
            return keyed;
        }

        var table = lower switch
        {
            "pr" => _pr,
            "run" or "session" => _run,
            "thread" or "note" => _thread,
            _ => _work,
        };
        return table[(int)state];
    }

    private static RowGlyph? ByKey(bool isPr, string? stateKey) => stateKey?.ToLowerInvariant() switch
    {
        "draft" => isPr ? RowGlyph.PrDraft : RowGlyph.Draft,
        "in-review" or "in_review" or "review" => RowGlyph.InReview,
        "ci-running" or "ci_running" => RowGlyph.CiRunning,
        "parked" => RowGlyph.Parked,
        "linked" => RowGlyph.Linked,
        "mention" => RowGlyph.Mention,
        _ => null,
    };
}
