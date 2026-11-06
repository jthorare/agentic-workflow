namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One required check (review page spec §6.2, §6.4): its state in a word and a glyph, its reason, and whether Send as fix task is offered.</summary>
internal sealed class ReviewCheckRowViewModel : ObservableObject
{
    /// <summary>The check's id.</summary>
    public string Id { get; } = string.Empty;

    /// <summary>What it asks.</summary>
    public string Title { get; } = string.Empty;

    /// <summary>The state's word: fail, needs a tick, running, claimed in body or pass.</summary>
    public string StatusWord { get; } = string.Empty;

    /// <summary>The state's glyph.</summary>
    public StateGlyph Glyph { get; } = StateGlyph.None;

    /// <summary>Why it stands so; empty for none.</summary>
    public string Detail { get; } = string.Empty;

    /// <summary>True when Send as fix task is offered (a failed check).</summary>
    public bool CanSendFix { get; }

    /// <summary>The screen-reader name: "Title: fail, reason".</summary>
    public string AutomationName { get; } = string.Empty;
}
