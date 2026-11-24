using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Views.Projects;

/// <summary>The Projects page: it tells its view model its own width, which sets which columns show; a row's click asks for its sheet, focus on a row selects it, the tabs and menus pick the view, sort and grouping, and a write that closed a sheet moves focus.</summary>
internal sealed partial class ProjectsPage : UserControl
{
    private ProjectsPageViewModel? _watched;

    public ProjectsPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Resize(e.NewSize.Width);
        DataContextChanged += (_, _) =>
        {
            Resize(Bounds.Width);
            Watch();
        };
        ProjectsRows.LayoutUpdated += (_, _) => UpdateStickyHead();   // a fold or a new read moves the heads without a scroll
        AddHandler(ProjectRow.OpenEvent, OnOpen);
        AddHandler(GotFocusEvent, OnRowFocused, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnRowKey, RoutingStrategies.Bubble);
    }

    // The Expander's inner toggle is what a screen reader lands on, and it announces expanded or collapsed itself, so its name is just the fold's visible words ("Finished (1)").
    // The Name is set here, not bound in the template, so the toggle keeps its ExpanderHeader name for drivers.
    private void OnFoldTemplate(object? sender, TemplateAppliedEventArgs e)
    {
        if (sender is not Expander { DataContext: ProjectFoldViewModel fold } || e.NameScope.Find<ToggleButton>("ExpanderHeader") is not { } toggle)
        {
            return;
        }

        AutomationProperties.SetName(toggle, fold.Heading);
        fold.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(ProjectFoldViewModel.Heading), StringComparison.Ordinal))
            {
                AutomationProperties.SetName(toggle, fold.Heading);
            }
        };
    }

    private void Resize(double width)
    {
        if (DataContext is ProjectsPageViewModel page && width > 0)
        {
            page.SetPageWidth(width);
        }
    }

    private void Watch()
    {
        if (_watched is { } old)
        {
            old.FocusRequested -= OnFocusRequested;
            old.MenuRequested -= OnMenuRequested;
            old.SearchRequested -= OnSearchRequested;
        }

        _watched = DataContext as ProjectsPageViewModel;
        if (_watched is { } now)
        {
            now.FocusRequested += OnFocusRequested;
            now.MenuRequested += OnMenuRequested;
            now.SearchRequested += OnSearchRequested;
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is ProjectRow { DataContext: ProjectCardViewModel card } && DataContext is ProjectsPageViewModel page)
        {
            page.RequestOpen(card.Link);
        }
    }

    // Focus on a row's frame makes it the row the keys act on.
    private void OnRowFocused(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Border { DataContext: ProjectCardViewModel card } frame && string.Equals(frame.Name, "ProjectRowFrame", StringComparison.Ordinal) && DataContext is ProjectsPageViewModel page)
        {
            _ = page.Select(card.Id);
        }
    }

    // On a row: Up and Down walk the rows like K and J, Enter opens the row's sheet. A key with a modifier is left alone.
    private void OnRowKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Source is not Border frame || !string.Equals(frame.Name, "ProjectRowFrame", StringComparison.Ordinal) || DataContext is not ProjectsPageViewModel page)
        {
            return;
        }

        e.Handled = PressRowKey(page, e.Key);
    }

#pragma warning disable IDE0072 // every other key is not the rows'
    private static bool PressRowKey(ProjectsPageViewModel page, Key key) => key switch
    {
        Key.Down => page.Press("J"),
        Key.Up => page.Press("K"),
        Key.Enter => page.Press("Enter"),
        _ => false,
    };
#pragma warning restore IDE0072

    // O and B open the Sort and the Group menu under their buttons, the way a click does.
    private void OnMenuRequested(object? sender, ProjectMenuRequestedEventArgs e)
    {
        var button = this.FindControl<Button>(e.Menu == ProjectMenu.Sort ? "ProjectsSort" : "ProjectsGroup");
        if (button is { IsEffectivelyVisible: true, Flyout: { } flyout })
        {
            flyout.ShowAt(button);
        }
    }

    // `/`: focus in the search field, posted (as the shell does for the Queue filter) so the key's own "/" is not typed into it.
    private void OnSearchRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = ProjectsSearch.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    // In the field: Esc clears it (then, empty, is left to the shell's Esc), Down goes to the list, Enter opens the selected row (the first when none is).
    private void OnSearchKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || DataContext is not ProjectsPageViewModel page)
        {
            return;
        }

        e.Handled = PressSearchKey(page, e.Key);
    }

#pragma warning disable IDE0072 // every other key is the search box's own
    private static bool PressSearchKey(ProjectsPageViewModel page, Key key) => key switch
    {
        Key.Escape => page.ClearSearch(),
        Key.Down => page.Press("J"),
        Key.Enter => (page.SelectedId is not null || page.Press("J")) && page.Press("Enter"),
        _ => false,
    };
#pragma warning restore IDE0072

    private void OnSearchFocus(object? sender, RoutedEventArgs e) => (DataContext as ProjectsPageViewModel)?.SearchFocused(ProjectsSearch.IsFocused);

    private void OnClearSearch(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectsPageViewModel page && page.ClearSearch())
        {
            _ = ProjectsSearch.Focus(NavigationMethod.Tab);
        }
    }

    private void OnRowsScrolled(object? sender, ScrollChangedEventArgs e) => UpdateStickyHead();

    // The group in view is the one whose head last passed the top edge of the rows; at the top (no head above the edge) the copy hides.
    private void UpdateStickyHead()
    {
        var edge = ProjectsScroll.Offset.Y - 0.5;
        ProjectGroupViewModel? current = null;
        var best = double.NegativeInfinity;
        foreach (var head in ProjectsGroups.GetVisualDescendants().OfType<Button>())
        {
            if (string.Equals(head.Name, "ProjectsGroupHead", StringComparison.Ordinal) && head is { IsEffectivelyVisible: true, DataContext: ProjectGroupViewModel group } && head.TranslatePoint(default, ProjectsRows) is { Y: var y } && y < edge && y > best)
            {
                (current, best) = (group, y);
            }
        }

        // Runs on every layout pass: change nothing when the group in view is the same, so the pass this causes ends the loop.
        if (!ReferenceEquals(ProjectsStickyHead.DataContext, current))
        {
            ProjectsStickyHead.DataContext = current;
        }

        if (ProjectsStickyHead.IsVisible != (current is not null))
        {
            ProjectsStickyHead.IsVisible = current is not null;
        }
    }

    private void OnGroupHead(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ProjectGroupViewModel group } && DataContext is ProjectsPageViewModel page)
        {
            page.ToggleGroup(group);
        }
    }

    private void OnTab(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ProjectViewTab tab } && DataContext is ProjectsPageViewModel page)
        {
            page.SetView(tab.View);
        }
    }

    private void OnChoice(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ProjectMenuChoice choice } && DataContext is ProjectsPageViewModel page)
        {
            page.Choose(choice);
        }
    }

    private void OnViewEmptyAction(object? sender, RoutedEventArgs e) => (DataContext as ProjectsPageViewModel)?.RunViewEmptyAction();

    private void OnAdd(object? sender, RoutedEventArgs e) => (DataContext as ProjectsPageViewModel)?.RequestAdd();

    private void OnConnect(object? sender, RoutedEventArgs e) => (DataContext as ProjectsPageViewModel)?.RequestConnect();

    // After the list is read again and the sheet has closed, or a key moved the selection: the row's frame, the Add button, or a fold's button. The empty block's
    // Add button stands in when the header's is not usable, and the page title when neither is.
    private void OnFocusRequested(object? sender, ProjectFocusEventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = Target(e)?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    private Control? Target(ProjectFocusEventArgs e) => e.Target switch
    {
        ProjectFocus.Card => RowFrame(e.CardId),
        ProjectFocus.HiddenFold => FoldButton("ProjectsHidden"),
        ProjectFocus.FinishedFold => FoldButton("ProjectsFinished"),
        ProjectFocus.Add => this.FindControl<Button>("ProjectsAdd") is { IsEnabled: true } add ? add : this.FindControl<TextBlock>("ProjectsTitle"),
        _ => null,
    };

    // The frame of the first drawn row of a project (a project under two repos is drawn twice).
    private Border? RowFrame(string? id) => this.GetVisualDescendants().OfType<ProjectRow>()
        .FirstOrDefault(r => r is { IsEffectivelyVisible: true, DataContext: ProjectCardViewModel card } && string.Equals(card.Id, id, StringComparison.Ordinal))?.FindControl<Border>("ProjectRowFrame");

    private ToggleButton? FoldButton(string fold) => this.FindControl<Expander>(fold)?.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault();
}
