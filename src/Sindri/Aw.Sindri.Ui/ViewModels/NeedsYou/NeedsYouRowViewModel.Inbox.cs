namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A Needs-you row (<see cref="NeedsYouRowViewModel"/>, continued): its second line in the inbox.</summary>
internal sealed partial class NeedsYouRowViewModel
{
    /// <summary>The inbox row's accessible name: the row's own, and "selected" while the card shows it.</summary>
    public string InboxName => IsSelected ? AccessibleName + ", " + Labels.NeedsYouSelectedWord : AccessibleName;

    /// <summary>The inbox row's one muted line: what a decision asks, else why the row needs the user, else its label.</summary>
    public string InboxLine => Entry.Question.Length > 0 ? Entry.Question : Entry.Reason.Length > 0 ? Entry.Reason : Entry.Label;
}
