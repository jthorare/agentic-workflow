using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Views.Todo;

/// <summary>Settings' Obsidian todo note card: a click or Enter only calls the view model; the no-vault button brings the Library card into view.</summary>
internal sealed partial class TodoNoteCardSection : UserControl
{
    private TodoNoteCardViewModel? _card;
    private (TodoNoteCardStage Stage, bool Confirming) _seen;

    public TodoNoteCardSection()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Detach();
        _card = DataContext as TodoNoteCardViewModel;
        if (TopLevel.GetTopLevel(this) is not null)
        {
            Attach();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Detach();
        base.OnDetachedFromVisualTree(e);
    }

    // The card outlives this view (Settings is rebuilt per visit), so the view hooks it only while it is on screen.
    private void Attach()
    {
        if (_card is null)
        {
            return;
        }

        Detach();
        _card.LibraryRequested += OnLibraryRequested;
        _card.PropertyChanged += OnCardChanged;
        _seen = (_card.Stage, _card.IsConfirmingStop);
    }

    private void Detach()
    {
        if (_card is null)
        {
            return;
        }

        _card.LibraryRequested -= OnLibraryRequested;
        _card.PropertyChanged -= OnCardChanged;
    }

    // A stage change moves focus to the first thing the new stage offers; the first load (Reading, no vault) leaves it where it is.
    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_card is not { } card || (card.Stage, card.IsConfirmingStop) == _seen)
        {
            return;
        }

        var (was, now) = (_seen, (card.Stage, card.IsConfirmingStop));
        _seen = now;
        if (was.Stage is TodoNoteCardStage.Reading or TodoNoteCardStage.NoVaults)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => FocusStage(now), DispatcherPriority.Loaded);   // after the bindings have drawn the new stage
    }

    private void FocusStage((TodoNoteCardStage Stage, bool Confirming) now)
    {
        var names = now switch
        {
            (TodoNoteCardStage.Checked, _) => new[] { "TodoNoteImport", "TodoNoteStart", "TodoNoteStartOneWay", "TodoNoteCancel" },
            (TodoNoteCardStage.Syncing, true) => ["TodoNoteKeep"],
            (TodoNoteCardStage.Syncing, false) => ["TodoNoteChange"],
            (TodoNoteCardStage.Form, _) => ["TodoNoteField"],
            _ => [],
        };
        _ = names.Select(this.FindControl<Control>).FirstOrDefault(c => c is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true })?.Focus();
    }

    // The Library card sits on the same Settings page, in the slot the Settings page names LibrarySettingsSlot.
    private void OnLibraryRequested(object? sender, EventArgs e) =>
        this.GetVisualAncestors().OfType<Control>().Select(a => a.FindControl<Control>("LibrarySettingsSlot")).FirstOrDefault(c => c is not null)?.BringIntoView();

    private void OnOpenLibrary(object? sender, RoutedEventArgs e) => _card?.OpenLibrary();

    private void OnCancel(object? sender, RoutedEventArgs e) => _card?.Cancel();

    private void OnChange(object? sender, RoutedEventArgs e) => _card?.Change();

    private void OnAskStop(object? sender, RoutedEventArgs e) => _card?.AskStop();

    private void OnKeepSyncing(object? sender, RoutedEventArgs e) => _card?.KeepSyncing();

    // A call that throws would end the process from an async void handler; the card shows an error instead.
#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
#pragma warning disable CA1031 // why: any failure of a click must land on the card, never on the dispatcher
    private async void Run(Func<TodoNoteCardViewModel, Task> call)
    {
        if (_card is not { } card)
        {
            return;
        }

        try
        {
            await call(card).ConfigureAwait(true);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            card.Crashed();
        }
    }
#pragma warning restore CA1031
#pragma warning restore MA0155

    private void OnCheck(object? sender, RoutedEventArgs e) => Run(c => c.Check(CancellationToken.None));

    private void OnStart(object? sender, RoutedEventArgs e) => Run(c => c.Start(CancellationToken.None));

    private void OnImport(object? sender, RoutedEventArgs e) => Run(c => c.Import(CancellationToken.None));

    private void OnStop(object? sender, RoutedEventArgs e) => Run(c => c.Stop(CancellationToken.None));

    private void OnNoteKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _card is { CanCheck: true })
        {
            e.Handled = true;
            Run(c => c.Check(CancellationToken.None));
        }
        else if (e.Key == Key.Escape && _card is { ShowCancel: true })
        {
            e.Handled = true;
            _card.Cancel();
        }
    }
}
