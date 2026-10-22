namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The Needs-you inbox page (<see cref="NeedsYouPageViewModel"/>, continued; F3 needs-you spec §3, slice 4): the answer form of a paused run and the takeover block of a taken-over one.</summary>
internal sealed partial class NeedsYouPageViewModel
{
    private const string ParkPrefix = "park:";
    private const string TakeoverPrefix = "takeover:";

    /// <summary>The answer form of a paused run; null when the page was built without one.</summary>
    public PausedAnswerViewModel? Answer { get; }

    /// <summary>The takeover block, its nudge and its toast; null when the page was built without one.</summary>
    public TakeoverViewModel? Takeover { get; }

    /// <summary>True when the card is a paused run and has the answer form.</summary>
    public bool ShowsAnswer => Answer is not null && !IsCleared && Card is { } row && row.Id.StartsWith(ParkPrefix, StringComparison.Ordinal);

    /// <summary>True when the card is a taken-over run and has the takeover block.</summary>
    public bool ShowsTakeover => Takeover is not null && !IsCleared && Card is { } row && row.Id.StartsWith(TakeoverPrefix, StringComparison.Ordinal);

    /// <summary>True while the action bar's own primary and terminal buttons show: a card with the answer form or the takeover block has one set of controls.</summary>
    public bool ShowsBarButtons => Bar.ShowsActions && !ShowsAnswer && !ShowsTakeover;

    // The card moved on: the form follows the row, and the takeover block reads the instance and takes the served rows (the nudge is one of them).
    private void ShowPausedRun()
    {
        Answer?.Show(Card?.Entry);
        if (Takeover is null)
        {
            return;
        }

        if (Card is { } row && row.Id.StartsWith(TakeoverPrefix, StringComparison.Ordinal) && row.Entry.Subject is { } subject)
        {
            _ = Takeover.Load(subject.Id);
        }

        Takeover.Observe([.. _list.Seen.Rows.Select(r => r.Entry)]);
    }
}
