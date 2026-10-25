using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>The Agents page: a title click opens the sheet, focus arriving selects the row, the filters narrow the list, and a key or a re-read can ask for focus.</summary>
internal sealed partial class AgentsPage : UserControl
{
    public AgentsPage()
    {
        InitializeComponent();
        AddHandler(SessionRow.OpenEvent, OnOpen);
        AddHandler(SessionRow.SelectEvent, OnSelect);
        AddHandler(EndedSessionRow.SelectEvent, OnSelectEnded);
        AddHandler(SessionGroup.ToggleEvent, OnToggle);
        SizeChanged += OnSized;
    }

    private bool _focusWasHere;

    // Under 640 px only Status, Agent and For stay: the page tells the rows, and the head's columns follow.
    private void OnSized(object? sender, SizeChangedEventArgs e)
    {
        if (Page is { } page)
        {
            page.Resize(e.NewSize.Width);
            AgentsColumnsGrid.ColumnDefinitions = ColumnDefinitions.Parse(page.IsCompact ? AgentColumnsLayout.Compact : AgentColumnsLayout.Wide);
        }
    }

    private void OnClearFilters(object? sender, RoutedEventArgs e)
    {
        ProviderFilter.SelectedIndex = 0;
        StateFilter.SelectedIndex = 0;
        RepoFilter.SelectedIndex = 0;
        Page?.ClearFilters();
    }

    private void OnOpenWorkflows(object? sender, RoutedEventArgs e) => Page?.RequestWorkflows();

    private AgentsPageViewModel? Page => DataContext as AgentsPageViewModel;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TopLevel.GetTopLevel(this)?.AddHandler(GotFocusEvent, OnFocusMoved, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        TopLevel.GetTopLevel(this)?.RemoveHandler(GotFocusEvent, OnFocusMoved);
        base.OnDetachedFromVisualTree(e);
    }

    // Where focus last went: a re-read only claims lost focus for the page when the human was last on it.
    // Focus going somewhere else with the sheet closed also drops the row's highlight, so a closed sheet leaves no row marked.
    private void OnFocusMoved(object? sender, RoutedEventArgs e)
    {
        _focusWasHere = e.Source is Visual source && this.IsVisualAncestorOf(source);
        if (!_focusWasHere && TopLevel.GetTopLevel(this)?.DataContext is ViewModels.Shell.ShellViewModel { IsSheetOpen: false })
        {
            Page?.ClearSelection();
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        Page?.FocusRequested -= OnFocusRequested;
        base.OnDataContextChanged(e);
        Page?.FocusRequested += OnFocusRequested;
    }

    private void OnFocusRequested(object? sender, AgentFocusEventArgs request) =>
        Dispatcher.UIThread.Post(() => Focus(request), DispatcherPriority.Loaded);

    // A key moved the selection onto a row (or O, on a row with no Step, onto its Focus line), or a re-read changed the rows under it: the row's frame takes focus on the
    // keyboard's ring and scrolls into view. A re-read only takes it when focus was lost with the old rows, never from a control the human is on.
    private void Focus(AgentFocusEventArgs request)
    {
        var frame = FrameOf(request.Key);
        if (frame is null || (!request.ToFocusLine && frame.IsKeyboardFocusWithin) || (request.OnlyIfLost && (HasUsableFocus() || !_focusWasHere)))
        {
            return;
        }

        var target = request.ToFocusLine ? frame.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "SessionFocusLine", StringComparison.Ordinal)) ?? frame : frame;
        _ = target.Focus(NavigationMethod.Tab);
        target.BringIntoView();
    }

    private Border? FrameOf(string key) =>
        this.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.IsEffectivelyVisible && IsFrame(b) && string.Equals(KeyOf(b), key, StringComparison.Ordinal));

    private static bool IsFrame(Border b) => string.Equals(b.Name, "SessionRowFrame", StringComparison.Ordinal) || string.Equals(b.Name, "EndedRowFrame", StringComparison.Ordinal);

    private static string KeyOf(Border frame) => frame.FindAncestorOfType<SessionRow>()?.Key ?? frame.FindAncestorOfType<EndedSessionRow>()?.Key ?? string.Empty;

    private bool HasUsableFocus() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } focused && TopLevel.GetTopLevel(focused) is not null;

    private void OnSelect(object? sender, RoutedEventArgs e)
    {
        if (e.Source is SessionRow { Key.Length: > 0 } row)
        {
            _ = Page?.Select(row.Key);
        }
    }

    private void OnSelectEnded(object? sender, RoutedEventArgs e)
    {
        if (e.Source is EndedSessionRow { Key.Length: > 0 } row)
        {
            _ = Page?.Select(row.Key);
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is SessionRow { Key.Length: > 0 } row && Page is { } page && page.Select(row.Key))
        {
            _ = page.Press("Enter");
        }
    }

    private void OnToggle(object? sender, RoutedEventArgs e) => (e.Source as SessionGroup)?.Group?.Toggle();

    private void OnEndedToggle(object? sender, RoutedEventArgs e) => Page?.ToggleEnded();

    private void OnProviderChosen(object? sender, SelectionChangedEventArgs e) => Page?.FilterProvider((sender as ComboBox)?.SelectedItem as string);

    private void OnStateChosen(object? sender, SelectionChangedEventArgs e) => Page?.FilterState((sender as ComboBox)?.SelectedItem as string);

    private void OnRepoChosen(object? sender, SelectionChangedEventArgs e) => Page?.FilterRepo((sender as ComboBox)?.SelectedItem as string);
}
