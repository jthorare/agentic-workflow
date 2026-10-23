namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The card's open actions (<see cref="NeedsYouPageViewModel"/>, continued; api q65): the pull request the selected request is about opens in the browser through the shell's launcher.</summary>
internal sealed partial class NeedsYouPageViewModel
{
    /// <summary>Raised when the card asks to open the pull request the request is about; the shell hands the address to the system.</summary>
    internal event EventHandler<NeedsYouRowEventArgs>? PrRequested;

    /// <summary>Opens the pull request the selected request is about, when it names one.</summary>
    internal void OpenPr()
    {
        if (Card is { HasPr: true } row)
        {
            PrRequested?.Invoke(this, new NeedsYouRowEventArgs(row.Id));
        }
    }
}
