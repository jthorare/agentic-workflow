using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
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
        TerminalPanel.ShowsList = false;
        TerminalPanel.PaneKeyPressed += (_, e) => _ = Page?.SendPaneKey(e.Name, e.Text);
        TerminalPanel.PanePasted += (_, e) => _ = Page?.SendPanePaste(e.Text);
        _tick.Tick += (_, _) => Page?.Terminal.Tick();
        TerminalPanel.TranscriptRequested += (_, _) => Page?.OpenTranscript();
        AddHandler(KeyDownEvent, OnEscape, RoutingStrategies.Bubble);
        DataContextChanged += (_, _) => FollowTerminal();
    }

    private const double NarrowBelow = 1100;

    private AgentsPageViewModel? _followed;

    // The terminal view's clock rules (retries, the 30 s rule, a key's wait) move while the view is open.
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(500) };

    // Esc with focus in the list closes the terminal view; focus in the pane never reaches here (the pane sends Esc to the agent).
    private void OnEscape(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && Page is { } page && page.Press("Esc"))
        {
            e.Handled = true;
        }
    }

    private void FollowTerminal()
    {
        _followed?.PropertyChanged -= OnPageChanged;
        _followed = Page;
        if (Page is { } drawn)
        {
            drawn.DrawFrame = grid => TerminalPanel.PaneDrawing.Draw(grid);
        }

        _followed?.PropertyChanged += OnPageChanged;
        Split();
    }

    private void OnPageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(AgentsPageViewModel.IsTerminalOpen), StringComparison.Ordinal))
        {
            Split();
        }
    }

    // Open: the list is the 360 px column (56 px under 1100 px of window width) and the pane takes the rest; closed: the list has the page.
    private void Split()
    {
        var open = Page?.IsTerminalOpen == true;
        _tick.IsEnabled = open;
        var narrow = Bounds.Width < NarrowBelow;
        AgentsSplit.ColumnDefinitions[0].Width = open ? new GridLength(narrow ? 56 : 360) : new GridLength(1, GridUnitType.Star);
        AgentsSplit.ColumnDefinitions[1].Width = open ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Page?.Resize(open ? (narrow ? 56 : 360) : Bounds.Width);
    }

    private bool _focusWasHere;

    // Under 640 px only Status, Agent and For stay: the page tells the rows, and the head's columns follow.
    private void OnSized(object? sender, SizeChangedEventArgs e)
    {
        Split();
    }

    private void OnClearFilters(object? sender, RoutedEventArgs e)
    {
        ProviderFilter.SelectedIndex = 0;
        StateFilter.SelectedIndex = 0;
        RepoFilter.SelectedIndex = 0;
        Page?.ClearFilters();
    }


    private AgentsPageViewModel? Page => DataContext as AgentsPageViewModel;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TopLevel.GetTopLevel(this)?.AddHandler(GotFocusEvent, OnFocusMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        (TopLevel.GetTopLevel(this)?.DataContext as ViewModels.Shell.ShellViewModel)?.PropertyChanged += OnShellChanged;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        TopLevel.GetTopLevel(this)?.RemoveHandler(GotFocusEvent, OnFocusMoved);
        (TopLevel.GetTopLevel(this)?.DataContext as ViewModels.Shell.ShellViewModel)?.PropertyChanged -= OnShellChanged;
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

    // The sheet closing may leave focus nowhere (no GotFocus follows): once the close has settled, a focus outside the page drops the row's highlight.
    private void OnShellChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ViewModels.Shell.ShellViewModel.IsSheetOpen), StringComparison.Ordinal) && sender is ViewModels.Shell.ShellViewModel { IsSheetOpen: false })
        {
            Dispatcher.UIThread.Post(DropHighlightIfFocusLeft, DispatcherPriority.Loaded);
        }
    }

    private void DropHighlightIfFocusLeft()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if ((focused is null || !this.IsVisualAncestorOf(focused)) && TopLevel.GetTopLevel(this)?.DataContext is ViewModels.Shell.ShellViewModel { IsSheetOpen: false })
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
#pragma warning restore MA0182
