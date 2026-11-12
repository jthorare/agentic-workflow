namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>
/// A read that failed or went stale, as the shared FailedReadLine draws it (#3056, audit #3019; grammar "Inline state line"): one row with a glyph,
/// one plain sentence and ONE primary button at the right end, labelled with the verb of the fix. The default is Retry; a page that has a better fix
/// ("Restart Sindri") passes it as the primary, and it replaces Retry. A raw code never goes in the sentence: it sits behind a Details disclosure.
/// </summary>
/// <param name="Words">What happened, in plain words, sentence case ("The queue can't be read just now.").</param>
/// <param name="Primary">What the primary button does; reads again unless the page passes a better fix.</param>
internal sealed record FailedRead(string Words, Func<CancellationToken, Task> Primary)
{
    /// <summary>The primary button's label: the verb of the fix.</summary>
    internal string PrimaryLabel { get; init; } = Labels.Retry;

    /// <summary>The API's or the app's code, shown only behind Details in mono; empty when there is none.</summary>
    internal string Code { get; init; } = string.Empty;

    /// <summary>True for a stale or stalled read (attention tint); false for a read that can't be read (error tint).</summary>
    internal bool IsAttention { get; init; }

    /// <summary>The optional ghost button's label; it is never the fix. Empty for none.</summary>
    internal string GhostLabel { get; init; } = string.Empty;

    /// <summary>What the ghost button does; null for none.</summary>
    internal Func<CancellationToken, Task>? Ghost { get; init; }

    /// <summary>True when a ghost button shows.</summary>
    internal bool HasGhost => Ghost is not null && GhostLabel.Length > 0;

    /// <summary>True when there is a code to put behind Details.</summary>
    internal bool HasCode => Code.Length > 0;

    /// <summary>The Details button's label.</summary>
    internal string DetailsLabel { get; init; } = Labels.FailedReadDetails;
}
