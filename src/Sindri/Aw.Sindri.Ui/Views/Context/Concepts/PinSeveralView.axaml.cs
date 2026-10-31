using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Views.Context.Concepts;

/// <summary>The Add several sheet (concept pins spec §3.6), bound to a <see cref="PinSeveralViewModel"/>: its buttons call the view model, and focus moves to the first row after Read the list.</summary>
#pragma warning disable MA0182 // Q7b: the shell sheet mounts it
internal sealed partial class PinSeveralView : UserControl
{
    private CancellationTokenSource? _lifetime;
    private PinSeveralViewModel? _bound;

    /// <summary>Builds the sheet.</summary>
    public PinSeveralView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => _lifetime?.Cancel();
    }

    private CancellationToken Token => (_lifetime ??= new CancellationTokenSource()).Token;

    // Reads the pins hash and what waits once the view model is known, and follows its row reads to move focus.
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _bound?.RowsRead -= OnRowsRead;
        _bound = DataContext as PinSeveralViewModel;
        if (_bound is not null)
        {
            _bound.RowsRead += OnRowsRead;
            _ = _bound.StartAsync(Token);
        }
    }

    private void OnReadList(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PinSeveralViewModel sheet)
        {
            _ = sheet.ReadListAsync(Token);
        }
    }

    private void OnSubmit(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PinSeveralViewModel sheet)
        {
            _ = sheet.PinAllAsync(Token);
        }
    }

    // A changed match reads that concept; the view model skips the read when the row already holds it.
    private void OnPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PinSeveralViewModel sheet && sender is Control { DataContext: PinSeveralRowViewModel row })
        {
            _ = sheet.RepickAsync(row, Token);
        }
    }

    // Focus goes to the first row's first control: its tick, or the next focusable one when the line couldn't be read.
    private void OnRowsRead(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(
            () => _ = PinSeveralRows.ContainerFromIndex(0)?.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(c => c.Focusable && c.IsEffectivelyEnabled)?.Focus(NavigationMethod.Tab),
            DispatcherPriority.Loaded);
}
#pragma warning restore MA0182
