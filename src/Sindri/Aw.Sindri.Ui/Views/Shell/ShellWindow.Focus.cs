using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Banner = Aw.Sindri.Ui.ViewModels.Shell.ViewsBanner;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Keeping focus somewhere sensible, and what the window listens to (<see cref="ShellWindow"/>, continued).</summary>
internal sealed partial class ShellWindow
{
    // Right, Left, Home or End pressed with focus on a nav tab selects a tab by itself (a tabs pattern with automatic
    // activation, review re-check Y1): the page changes and focus stays on the tab. Any other key, or a pointer press, clears it.
    private bool _navArrowing;

    // Avalonia's ListBox moves focus to the next tab on an arrow with NavigationMethod.Unspecified, which draws no focus ring
    // (no :focus-visible; review re-check R2, WCAG 2.4.7). A key the nav handled is remembered here so the tab it focused is
    // focused again as a keyboard focus (Directional).
    private bool _navKey;

    private void WatchNavigationKeys()
    {
        AddHandler(KeyDownEvent, OnNavigationKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => _navArrowing = _navKey = false, RoutingStrategies.Tunnel);
        TopBar.Get<ListBox>("Navigation").AddHandler(GotFocusEvent, OnNavigationGotFocus, RoutingStrategies.Bubble);
    }

    private void OnNavigationKeyDown(object? sender, KeyEventArgs e) =>
        _navArrowing = _navKey = e is { Key: Key.Left or Key.Right or Key.Home or Key.End, KeyModifiers: KeyModifiers.None }
            && FocusManager?.GetFocusedElement() is Visual focused
            && TopBar.Get<ListBox>("Navigation").IsVisualAncestorOf(focused);

    private void OnNavigationGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (!_navKey || e.NavigationMethod != NavigationMethod.Unspecified || e.Source is not ListBoxItem tab)
        {
            return;
        }

        _navKey = false;
        Dispatcher.UIThread.Post(() => RefocusAsKeyboard(tab), DispatcherPriority.Loaded);
    }

    // Focus on the element it is already on does nothing, so it goes to the health pill (next to the tabs) for a moment first.
    private void RefocusAsKeyboard(ListBoxItem tab)
    {
        if (ReferenceEquals(FocusManager?.GetFocusedElement(), tab) && Usable(TopBar.Get<Control>("HealthPill")) is { } pill)
        {
            _ = pill.Focus(NavigationMethod.Directional);
            _ = tab.Focus(NavigationMethod.Directional);
        }
    }

    private void Listen(ShellViewModel? shell, bool on)
    {
        if (shell is null)
        {
            return;
        }

        ListenNeedsYou(shell, on);
        ListenStrip(shell, on);
        ListenWorkflows(shell, on);
        ListenAgents(shell, on);
        if (on)
        {
            shell.Queue.FocusRequested += OnFocusRow;
            shell.SheetStacked += OnSheetStacked;
        }
        else
        {
            shell.Queue.FocusRequested -= OnFocusRow;
            shell.SheetStacked -= OnSheetStacked;
        }

        PropertyChangedEventHandler[] handlers = [OnShellChanged, OnBannerChanged, OnOverlayChanged, OnOverlayChanged, OnOverlayChanged, OnOverlayChanged, OnOverlayChanged, OnQueueChanged];
        INotifyPropertyChanged[] sources = [shell, shell.Health.Banner, shell.Palette, shell.Shortcuts, shell.Create, shell.Start, shell.ApproveAll, shell.Queue];
        for (var i = 0; i < sources.Length; i++)
        {
            if (on)
            {
                sources[i].PropertyChanged += handlers[i];
            }
            else
            {
                sources[i].PropertyChanged -= handlers[i];
            }
        }
    }

    // The views banner carries Rebuild views: when it goes, or the control goes with a change of banner, focus on it moves on.
    private void OnBannerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(Banner.IsShown), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(Banner.HasRebuild), StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        }
    }

    // The "N new" pill hides when what it held is shown, also when the pointer leaves: focus on it moves on.
    private void OnQueueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(QueueViewModel.HasHeld), StringComparison.Ordinal) && Shell?.Queue.HasHeld == false)
        {
            Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        }
    }

    // The queue asks for focus on a row (J / K, or the row a refresh moved the selection to): the row's frame gets it, on
    // the keyboard's ring, once the refresh has laid the rows out. A refresh only takes it when focus was lost with the old row.
    private void OnFocusRow(object? sender, FocusRequestedEventArgs request)
    {
        _rowRequests++;   // KeepFocus (posted by whatever hid the focused control) waits for this: focus moves once
        Dispatcher.UIThread.Post(() => FocusRow(request), DispatcherPriority.Loaded);
    }

    private void FocusRow(FocusRequestedEventArgs request)
    {
        _rowRequests--;
        if (request.OnlyIfLost && HasUsableFocus)
        {
            return;
        }

        if (RowFrame(request.ItemId) is not { } frame)
        {
            KeepFocus();   // the row is not drawn: whoever KeepFocus waited for still gets its neighbour
            return;
        }

        _ = frame.Focus(NavigationMethod.Tab);
    }

    // The visible frame of the row for an item, on the page the window shows.
    private Control? RowFrame(string itemId) => Usable(this.Get<Control>("Page").GetVisualDescendants().OfType<Border>()
        .FirstOrDefault(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal) && b.IsEffectivelyVisible
            && b.DataContext is ItemRowViewModel row && string.Equals(row.Id, itemId, StringComparison.Ordinal)));

    // A page change made by a navigation command (a G-chord, the palette's Go to, a nav tab) always lands focus on the new
    // page's region, wherever focus was: the top bar, a nav item, a sheet beside the page (review run 5, a11y-0). Focus
    // already on the region would not move, so it is dropped and taken again to raise a fresh event for the new name
    // (run 4, a11y-2). Over a covering sheet the page is disabled and cannot take focus: the usual keep-focus rule applies.
    private void KeepFocusOrReannounce()
    {
        // A pointer click on a top-bar tab stays on the tab (ux-C-10b): the region is for keyboard, chord and palette navigation, which have nowhere else to put focus.
        if (_lastMethod == NavigationMethod.Pointer && FocusManager?.GetFocusedElement() is Visual clicked && TopBar.Get<ListBox>("Navigation").IsVisualAncestorOf(clicked))
        {
            return;
        }

        var page = this.Get<Border>("Page");
        if (!page.IsEffectivelyEnabled)
        {
            KeepFocus();
            return;
        }

        if (ReferenceEquals(FocusManager?.GetFocusedElement(), page))
        {
            // Avalonia drops focus from an element that is disabled and has no clear call: disable and enable it, then focus again.
            var enabled = page.IsEnabled;
            page.SetCurrentValue(IsEnabledProperty, value: false);
            page.SetCurrentValue(IsEnabledProperty, enabled);
        }

        _sheetOpener = null;
        _ = page.Focus(Method());
    }

    // After a state change, a closed sheet or a change in its covering: if the focused control is gone (hidden, disabled,
    // or nothing has focus after something had it), focus its region's neighbour.
    private void KeepFocus()
    {
        // A row request is pending (the pill pressed: its row asks for focus right after the pill hides): it moves focus, once.
        if (_rowRequests > 0 || HasUsableFocus || _lastFocused is not { } last)
        {
            return;
        }

        var target = Neighbour(last) ?? Usable(FirstFocusable(TopBar) as Control);
        _sheetOpener = null;
        _ = target?.Focus(Method());
    }

    // The sheet's opener (the row it was opened from, by id, or the edge tab), else the row the selection moved to when the opener cleared, else the page region for the sheet; the health pill for the top bar; the page
    // region, or the sheet's heading while the sheet covers the page.
    private Control? Neighbour(Control last)
    {
        var page = Usable(this.Get<Control>("Page")) ?? (Covering ? Usable(this.Get<Border>("SheetTitle")) : null);
        if (SheetPanel == last || SheetPanel.IsVisualAncestorOf(last))
        {
            return SheetOpenerFrame() ?? page;
        }

        return TopBar.IsVisualAncestorOf(last) ? Usable(TopBar.Get<Control>("HealthPill")) : page;
    }

    private Control? OpenerRowFrame() => Shell?.OpenerRow is { } id ? RowFrame(id) : null;

    private Control? SelectedRowFrame() => Shell?.Queue.SelectedId is { } id ? RowFrame(id) : null;

    private static Control? Usable(Control? control) =>
        control is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Focusable: true } ? control : null;

    // Opening the sheet over the page (below 1100 px) moves focus to its heading (§11.2). Beside the page focus stays
    // where it is, unless what had it hid as the sheet opened (the edge tab at 1100 px and wider; review run 4,
    // accessibility-tester-0): then the heading gets it too, never a hidden control.
    private void FocusOpenedSheet()
    {
        if (Shell is { Start.IsOpen: true } or { ApproveAll.IsOpen: true })
        {
            return;   // the Start form is modal: an instance it opened (an existing attempt) stays behind it, and focus stays in the form
        }

        if (Shell is { IsSheetShown: true } shell && (Covering || !HasRealFocus))
        {
            _ = this.Get<Border>("SheetTitle").Focus(Method());
            if (NoOpener(shell))
            {
                EnterOpenedFrame(shell);   // a link with no row behind it goes on to the frame; a row's open stays on the heading (§11.2)
            }
        }
        else if (Shell is { IsSheetShown: true, IsInstanceSheet: true } opened && NoOpener(opened))
        {
            EnterOpenedFrame(opened);   // a link with no row behind it (a deep link, a pointer): nothing on the page to stay on
        }
    }

    // A tab was selected without the keyboard's arrows: focus rests on the selected tab (a pointer press already put it there; a screen reader's or the driver's activation did not).
    private void FocusSelectedTab()
    {
        var nav = TopBar.Get<ListBox>("Navigation");
        if (FocusManager?.GetFocusedElement() is Visual focused && nav.IsVisualAncestorOf(focused))
        {
            return;
        }

        _ = Usable(nav.ContainerFromIndex(nav.SelectedIndex))?.Focus(Method());
    }

    // The page region takes focus (named, so the page is spoken); the Context page then sends it on to its anchor box. One path for a page change and the window opening.
    private void LandOnPage()
    {
        Dispatcher.UIThread.Post(KeepFocusOrReannounce, DispatcherPriority.Loaded);
        FocusContextAnchor();
    }

    // Focus on a visible, enabled control. The window itself is no focus: an activated desktop window puts focus on its own scope first.
    private bool HasRealFocus => HasUsableFocus && FocusManager?.GetFocusedElement() is not ShellWindow;

    private bool HasUsableFocus => FocusManager?.GetFocusedElement() is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true };
}
