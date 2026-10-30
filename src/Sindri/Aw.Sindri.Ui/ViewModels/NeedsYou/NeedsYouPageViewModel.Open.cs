using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The card's open actions (<see cref="NeedsYouPageViewModel"/>, continued; api q65): the pull request the selected request is about opens in the browser through the shell's launcher.</summary>
internal sealed partial class NeedsYouPageViewModel
{
    /// <summary>Raised when the card asks to open the pull request the request is about; the shell hands the address to the system.</summary>
    internal event EventHandler<NeedsYouRowEventArgs>? PrRequested;

    /// <summary>The pull request link's launch; its failed line shows under the card's link.</summary>
    public LinkLaunch PrLaunch { get; private set; } = new LinkLaunch(NullLauncher.Instance);

    /// <summary>Hands a pull request link to the system and keeps the answer in <see cref="PrLaunch"/>.</summary>
    /// <param name="uri">The pull request.</param>
    /// <returns>A task that ends once the system answered.</returns>
    internal Task OpenPrLink(Uri uri) => PrLaunch.Open(uri, CancellationToken.None);

    /// <summary>Opens the pull request the selected request is about, when it names one.</summary>
    internal void OpenPr()
    {
        if (Card is { HasPr: true } row)
        {
            PrRequested?.Invoke(this, new NeedsYouRowEventArgs(row.Id));
        }
    }
}
