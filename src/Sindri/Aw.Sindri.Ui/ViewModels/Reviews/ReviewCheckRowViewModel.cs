using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One required check (review page spec §6.2, §6.4): its state in a word and a glyph, its reason, and whether Send as fix task is offered.</summary>
internal sealed class ReviewCheckRowViewModel : ObservableObject
{
    internal ReviewCheckRowViewModel(ReviewCheckView check)
    {
        (Id, Title, Detail, CanSendFix) = (check.Id, check.Title, check.Detail ?? string.Empty, check.FixTask);
        (StatusWord, Glyph) = check.Status switch
        {
            "fail" => (Labels.ReviewCheckFail, StateGlyph.Down),
            "needsTick" => (Labels.ReviewCheckNeedsTick, StateGlyph.Warning),
            "running" => (Labels.ReviewCheckRunning, StateGlyph.Stale),
            "claimed" => (Labels.ReviewCheckClaimed, StateGlyph.Profile),
            _ => (Labels.ReviewCheckPass, StateGlyph.Ok),
        };
        AutomationName = Labels.ReviewCheckName(Title, StatusWord, check.Detail);
    }

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The fix button's words.</summary>
    public string FixText => Labels.ReviewFixHeading;
#pragma warning restore CA1822, MA0041

    /// <summary>The fix button's screen-reader name, with the row's title.</summary>
    public string FixName => Labels.ReviewFixFor(Title);

    /// <summary>The check's id.</summary>
    public string Id { get; }

    /// <summary>What it asks.</summary>
    public string Title { get; }

    /// <summary>The state's word: fail, needs a tick, running, claimed in body or pass.</summary>
    public string StatusWord { get; }

    /// <summary>The state's glyph.</summary>
    public StateGlyph Glyph { get; }

    /// <summary>Why it stands so; empty for none.</summary>
    public string Detail { get; }

    /// <summary>True when Send as fix task is offered (a failed check).</summary>
    public bool CanSendFix { get; }

    /// <summary>The screen-reader name: "Title: fail, reason".</summary>
    public string AutomationName { get; }
}
