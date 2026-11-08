using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Views.Todo;

/// <summary>Settings' Obsidian todo note card: a click or Enter only calls the view model; the no-vault button brings the Library card into view.</summary>
internal sealed partial class TodoNoteCardSection : UserControl
{
    private TodoNoteCardViewModel? _card;

    public TodoNoteCardSection()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _card?.LibraryRequested -= OnLibraryRequested;
        _card = DataContext as TodoNoteCardViewModel;
        _card?.LibraryRequested += OnLibraryRequested;
    }

    // The Library card sits on the same Settings page, in the slot the Settings page names LibrarySettingsSlot.
    private void OnLibraryRequested(object? sender, EventArgs e) =>
        this.GetVisualAncestors().OfType<Control>().Select(a => a.FindControl<Control>("LibrarySettingsSlot")).FirstOrDefault(c => c is not null)?.BringIntoView();

    private void OnOpenLibrary(object? sender, RoutedEventArgs e) => _card?.OpenLibrary();

    private void OnCancel(object? sender, RoutedEventArgs e) => _card?.Cancel();

    private void OnChange(object? sender, RoutedEventArgs e) => _card?.Change();

    private void OnAskStop(object? sender, RoutedEventArgs e) => _card?.AskStop();

    private void OnKeepSyncing(object? sender, RoutedEventArgs e) => _card?.KeepSyncing();

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnCheck(object? sender, RoutedEventArgs e)
    {
        if (_card is not null)
        {
            await _card.Check(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnStart(object? sender, RoutedEventArgs e)
    {
        if (_card is not null)
        {
            await _card.Start(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        if (_card is not null)
        {
            await _card.Import(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnStop(object? sender, RoutedEventArgs e)
    {
        if (_card is not null)
        {
            await _card.Stop(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnNoteKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _card is { CanCheck: true })
        {
            e.Handled = true;
            await _card.Check(CancellationToken.None).ConfigureAwait(true);
        }
        else if (e.Key == Key.Escape && _card is { ShowCancel: true })
        {
            e.Handled = true;
            _card.Cancel();
        }
    }
#pragma warning restore MA0155
}
