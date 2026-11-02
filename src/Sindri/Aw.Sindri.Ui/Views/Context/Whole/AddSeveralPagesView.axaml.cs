using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The Add several pages sheet (pages spec §3.6), bound to an <see cref="AddSeveralPagesViewModel"/>: focus starts on the paste box, a read moves it to the first row with a reason or to Save all, and Esc answers the sheet from anywhere inside it.</summary>
#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
internal sealed partial class AddSeveralPagesView : UserControl
{
    private AddSeveralPagesViewModel? _watched;

    /// <summary>Builds the sheet.</summary>
    public AddSeveralPagesView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FocusFirst();
    }

    private AddSeveralPagesViewModel? Sheet => DataContext as AddSeveralPagesViewModel;

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && Sheet is { } sheet)
        {
            e.Handled = sheet.Escape();   // a text box in the sheet is where Esc is typed; the page's own handler skips those
        }
    }

    private void Watch()
    {
        _watched?.FocusRequested -= OnFocusRequested;
        _watched = Sheet;
        if (_watched is { } now)
        {
            now.FocusRequested += OnFocusRequested;
            FocusFirst();
        }
    }

    private void OnFocusRequested(object? sender, ValueEventArgs<AddSeveralPagesFocus> e)
    {
        switch (e.Value)
        {
            case AddSeveralPagesFocus.Text:
                FocusLater(AddSeveralText);
                break;
            case AddSeveralPagesFocus.FirstRow:
                Dispatcher.UIThread.Post(FocusFirstRow, DispatcherPriority.Loaded);
                break;
            case AddSeveralPagesFocus.KeepEditing:
                FocusLater(AddSeveralKeepEditing);
                break;
            case AddSeveralPagesFocus.ProblemRow:
                Dispatcher.UIThread.Post(FocusProblemRow, DispatcherPriority.Loaded);
                break;
            case AddSeveralPagesFocus.SaveAll:
            default:
                FocusLater(AddSeveralSaveAll);
                break;
        }
    }

    // The paste box starts with focus; Accept all suggested has none, so focus waits for its rows (the Save all button until they come).
    private void FocusFirst()
    {
        if (Sheet is { HasPasteBox: false })
        {
            FocusLater(AddSeveralSaveAll);
            return;
        }

        FocusLater(AddSeveralText);
    }

    // The first row's tick takes focus, else Save all.
    private void FocusFirstRow()
    {
        var tick = AddSeveralRows.ContainerFromIndex(0)?.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(static c => string.Equals(c.Name, "AddSeveralTick", StringComparison.Ordinal));
        _ = (tick ?? (Control)AddSeveralSaveAll).Focus(NavigationMethod.Tab);
    }

    // The first row with a problem takes focus: on its reason line, which a screen reader reads, or on its concept picker.
    private void FocusProblemRow()
    {
        if (Sheet is { ProblemRow: >= 0 } sheet && AddSeveralRows.ContainerFromIndex(sheet.ProblemRow) is { } container)
        {
            // A row with a concept to pick takes focus on its first picker; any other on its reason line.
            var descendants = container.GetVisualDescendants().ToList();
            var target = (Control?)descendants.OfType<ComboBox>().FirstOrDefault(static c => string.Equals(c.Name, "AddSeveralPick", StringComparison.Ordinal))
                ?? descendants.OfType<TextBlock>().FirstOrDefault(static t => string.Equals(t.Name, "AddSeveralReason", StringComparison.Ordinal));
            _ = target?.Focus(NavigationMethod.Tab);
        }
    }

    private static void FocusLater(Control control) => Dispatcher.UIThread.Post(() => _ = control.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    private void OnRead(object? sender, RoutedEventArgs e) => _ = Sheet?.ReadList(CancellationToken.None);

    private void OnSaveAll(object? sender, RoutedEventArgs e) => _ = Sheet?.SaveAll(CancellationToken.None);

    private void OnClose(object? sender, RoutedEventArgs e) => Sheet?.Escape();

    private void OnDiscard(object? sender, RoutedEventArgs e) => Sheet?.Close();

    private void OnKeepEditing(object? sender, RoutedEventArgs e) => Sheet?.KeepEditing();
}
#pragma warning restore MA0182
