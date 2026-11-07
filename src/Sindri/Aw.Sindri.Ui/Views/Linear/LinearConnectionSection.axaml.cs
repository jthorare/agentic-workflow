using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.Views.Linear;

/// <summary>Settings' Linear card: a click or Enter only calls the view model; a reveal request brings the card into view and focuses its first field.</summary>
internal sealed partial class LinearConnectionSection : UserControl
{
    private LinearConnectionViewModel? _card;

    public LinearConnectionSection()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _card?.RevealRequested -= OnRevealRequested;
        _card = DataContext as LinearConnectionViewModel;
        _card?.RevealRequested += OnRevealRequested;
    }

    // Opened by Connect: once the layout has run the card is scrolled into view, and focus goes to the key the owner has to paste next: the read key, else the write key,
    // else (both set) the write line's Replace key.
    private void OnRevealRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            LinearConnectionCard.BringIntoView();
            Control target = _card switch
            {
                { ShowReadPaste: true } => LinearReadKey,
                { ShowWritePaste: true } => LinearWriteKey,
                _ => LinearWriteReplace,
            };
            _ = target.Focus();
        }, DispatcherPriority.Loaded);

    private void OnReview(object? sender, RoutedEventArgs e) => _card?.RequestReview();

    private void OnReplaceRead(object? sender, RoutedEventArgs e) => _card?.ReplaceRead();

    private void OnReplaceWrite(object? sender, RoutedEventArgs e) => _card?.ReplaceWrite();

    private void OnAskRemove(object? sender, RoutedEventArgs e) => _card?.AskRemove();

    private void OnKeepWrite(object? sender, RoutedEventArgs e) => _card?.KeepWrite();

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnListTeams(object? sender, RoutedEventArgs e)
    {
        if (_card is not null)
        {
            await _card.ListTeams(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnSaveRead(object? sender, RoutedEventArgs e)
    {
        if (_card is not null)
        {
            await _card.SaveRead(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnCheckWrite(object? sender, RoutedEventArgs e)
    {
        if (_card is not null)
        {
            await _card.CheckWrite(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnRemoveWrite(object? sender, RoutedEventArgs e)
    {
        if (_card is not null)
        {
            await _card.RemoveWrite(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnReadKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _card is { CanListTeams: true })
        {
            e.Handled = true;
            await _card.ListTeams(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnWriteKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _card is { CanCheckWrite: true })
        {
            e.Handled = true;
            await _card.CheckWrite(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
