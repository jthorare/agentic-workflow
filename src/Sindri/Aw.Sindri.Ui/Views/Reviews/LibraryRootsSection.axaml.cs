using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Views.Reviews;

/// <summary>Settings' Library card: a click or Enter only calls the view model; a refusal moves focus to the field it names.</summary>
internal sealed partial class LibraryRootsSection : UserControl
{
    private LibraryRootFormViewModel? _form;

    public LibraryRootsSection()
    {
        InitializeComponent();
    }

    private LibraryRootsViewModel? Card => DataContext as LibraryRootsViewModel;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _form?.PropertyChanged -= OnFormChanged;
        _form = Card?.Form;
        _form?.PropertyChanged += OnFormChanged;
    }

    // The form opens on its name; a refusal puts focus on the field it names.
    private void OnFormChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var target = e.PropertyName switch
        {
            nameof(LibraryRootFormViewModel.IsOpen) when _form is { IsOpen: true } => LibraryFormName,
            nameof(LibraryRootFormViewModel.NameError) when _form is { HasNameError: true } => LibraryFormName,
            nameof(LibraryRootFormViewModel.PathError) when _form is { HasPathError: true } => LibraryFormPath,
            nameof(LibraryRootFormViewModel.DenyError) when _form is { HasDenyError: true } => LibraryFormDeny,
            _ => null,
        };
        if (target is not null)
        {
            Dispatcher.UIThread.Post(() => target.Focus(), DispatcherPriority.Input);
        }
    }

    private static LibraryFolderViewModel? RowOf(object? sender) => (sender as Control)?.DataContext as LibraryFolderViewModel;

    private void OnAdd(object? sender, RoutedEventArgs e) => Card?.OpenForm();

    private void OnCancel(object? sender, RoutedEventArgs e) => Card?.CloseForm();

    private void OnRemoveAsk(object? sender, RoutedEventArgs e) => RowOf(sender)?.AskRemove();

    private void OnRemoveCancel(object? sender, RoutedEventArgs e) => RowOf(sender)?.CancelRemove();

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnRemoveConfirm(object? sender, RoutedEventArgs e)
    {
        if (Card is { } card && RowOf(sender) is { } row)
        {
            await card.Remove(row, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnTurnOff(object? sender, RoutedEventArgs e)
    {
        if (Card is { } card)
        {
            await card.TurnAw(on: false, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnTurnOn(object? sender, RoutedEventArgs e)
    {
        if (Card is { } card)
        {
            await card.TurnAw(on: true, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        if (_form is not null)
        {
            await _form.Browse(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_form is not null)
        {
            await _form.Save(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnFieldKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _form is { CanSave: true })
        {
            e.Handled = true;
            await _form.Save(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
