using System.ComponentModel;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The Needs-you inbox page (<see cref="NeedsYouPageViewModel"/>, continued; F3 needs-you spec §2, slice 2): the action bar under the card, the done card, and the key hints.</summary>
internal sealed partial class NeedsYouPageViewModel
{
    /// <summary>The action bar under the card (slice 2): Approve, Send back, the terminal button and the keys behind them.</summary>
    public NeedsYouBarViewModel Bar { get; }

    /// <summary>True when the card shows the request: it is not replaced by the done card.</summary>
    public bool ShowsRequest => HasSelected && !Bar.IsApproved;

    /// <summary>True when the card is replaced by the done card (the request was approved, sent back or dropped).</summary>
    public bool IsDone => HasSelected && Bar.IsApproved;

    /// <summary>Asks the page to move focus to the card, so the arrow keys read it (Enter on the list).</summary>
    internal event EventHandler? CardFocusRequested;

    /// <summary>Enter: focus goes to the card.</summary>
    internal void FocusCard() => CardFocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The key hint of Enter.</summary>
    public string KeyOpenWord { get; } = Labels.NeedsYouKeyOpenWord;

    /// <summary>The key hint of A.</summary>
    public string KeyApproveWord { get; } = Labels.NeedsYouKeyApproveWord;

    /// <summary>The key hint of D.</summary>
    public string KeySendBackWord { get; } = Labels.NeedsYouKeySendBackWord;

    // The bar answered or reset: the card or the done card shows.
    private void OnBarChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(NeedsYouBarViewModel.IsApproved), StringComparison.Ordinal))
        {
            OnChanged(nameof(ShowsRequest));
            OnChanged(nameof(IsDone));
        }
        else if (string.Equals(e.PropertyName, nameof(NeedsYouBarViewModel.ShowsActions), StringComparison.Ordinal))
        {
            OnChanged(nameof(ShowsBarButtons));
        }
    }
}
