namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One part of the summary strip (review page spec §6.2, §16): its words, the panel it jumps to, and its screen-reader name.</summary>
internal sealed class ReviewStripSegmentViewModel : ObservableObject
{
    /// <summary>The panel the part jumps to; empty for a placeholder.</summary>
    public string Panel { get; } = string.Empty;

    /// <summary>The part's words ("3 open", "Covers Ph5, REQ-E3", "Reading checks…").</summary>
    public string Text { get; } = string.Empty;

    /// <summary>The part's state glyph.</summary>
    public StateGlyph Glyph { get; } = StateGlyph.None;

    /// <summary>True for a placeholder while the page loads.</summary>
    public bool IsLoading { get; }

    /// <summary>The screen-reader name: the panel's title and the words.</summary>
    public string AutomationName { get; } = string.Empty;
}
