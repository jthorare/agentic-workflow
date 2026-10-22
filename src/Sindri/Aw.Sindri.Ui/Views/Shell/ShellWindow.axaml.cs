using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// The shell window (UI spec §3.2): binds to the shell view model. The code here is view plumbing only: the keys of §8
/// that U0 serves (F6 and Shift+F6 cycle top bar, page and sheet, and on macOS ⌘⌥→ and ⌘⌥← too; Esc closes the sheet,
/// or Settings over the Not running card; ⌘, opens Settings), the sheet's drag and its covering below 1100 px, handing
/// the window's bounds and the sheet's width to the view model to remember, and keeping focus somewhere sensible (§11.2)
/// when the focused control disappears: closing the sheet returns it to what opened the sheet (the edge tab) or the page
/// region, and a state change that hides it moves it to its region's neighbour (the health pill in the top bar, the page
/// region in the page). It never goes nowhere.
/// </summary>
internal sealed partial class ShellWindow : Window
{
    private static readonly string[] _regions = ["TopBar", "Page", "Sheet"];

    // ⌘ on macOS, Ctrl elsewhere (§8: ⌘, opens Settings).
    private static readonly KeyModifiers _command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    private ShellViewModel? _shell;
    private Control? _lastFocused;
    private int _rowRequests;   // row-focus requests posted and not yet run
    private NavigationMethod _lastMethod = NavigationMethod.Tab;
    private Control? _sheetOpener;

    // What had focus when the palette or the ? sheet opened: it gets focus back when they close (§8, §11.2).
    private Control? _overlayOpener;

    public ShellWindow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, OnGotFocus, handledEventsToo: true);
        PagePanel.SizeChanged += (_, e) => PageLayout.SetTier(PagePanel, PageLayout.For(e.NewSize.Width));
        WatchNavigationKeys();
    }

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    private Border SheetPanel => this.Get<Border>("Sheet");

    private Border PagePanel => this.Get<Border>("Page");

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // A relaunch restores the last page before the window exists, so no page change lands focus on its region: do it once the window is open.
        Dispatcher.UIThread.Post(() =>
        {
            // A sheet a deep link opened meanwhile (IsSheetOpen: the shell is not connected yet, so IsSheetShown is still false) has its own focus rule (FocusOpenedSheet).
            if (!HasRealFocus && Shell is not { IsSheetOpen: true })
            {
                LandOnPage();
            }
        }, DispatcherPriority.Loaded);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        Listen(_shell, on: false);
        _shell = Shell;
        Listen(_shell, on: true);
        if (_shell?.Window is { } bounds)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = PixelPoint.FromPoint(new Point(bounds.X, bounds.Y), DesktopScaling);
            Width = bounds.Width;
            Height = bounds.Height;
        }

        Cover(Bounds.Width);
        base.OnDataContextChanged(e);
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        Cover(e.NewSize.Width);
        base.OnSizeChanged(e);
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        RememberBounds();
        base.OnClosing(e);
    }

    /// <summary>Hands the window's bounds to the view model to remember (on closing, and before the app ends).</summary>
    internal void RememberBounds()
    {
        var at = Position.ToPoint(DesktopScaling);
        Shell?.RememberWindow(new WindowBounds(at.X, at.Y, ClientSize.Width, ClientSize.Height));
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        var chordWaiting = Shell?.IsChordPending == true;
        if (!IsChordStep(e))
        {
            Shell?.CancelChord();
        }

        // Esc with a chord waiting is spent on cancelling it: whatever Esc would close stays open.
        e.Handled = RegionStep(e.Key, e.KeyModifiers, OperatingSystem.IsMacOS()) is { } step ? CycleRegion(back: step < 0)
            : e.Key == Key.Escape ? chordWaiting || Escape()
            : TabOutOfOpenRow(e) || OpenByKey(e) || (!OverlayOpen && PageKey(e));
        base.OnKeyDown(e);
    }

    // A chord's keys are the plain letters and '.'; a modifier pressed alone is part of typing a key, not another key.
    private static bool IsChordStep(KeyEventArgs e) =>
        (e.KeyModifiers == KeyModifiers.None && ChordKey(e.Key) is not null)
        || e.Key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    // Below Size.SheetCoversBelow the sheet covers the page, with a back affordance; otherwise it keeps its width. When
    // an open sheet starts or stops covering (the window narrowed or widened), focus left on the page it now disables
    // moves on (review run 4, accessibility-tester-1).
    private void Cover(double width)
    {
        var below = this.TryFindResource("Size.SheetCoversBelow", out var value) && value is double limit ? limit : 1100;
        var covers = width > 0 && width < below;
        var changed = covers != SheetPanel.Classes.Contains("covers");
        SheetPanel.Classes.Set("covers", covers);
        SheetPanel.Width = covers ? double.NaN : Shell?.ShownSheetWidth ?? double.NaN;
        CoverPage();
        if (changed && Shell is { IsSheetShown: true })
        {
            Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        }
    }

    // While the sheet covers the page, the page and its controls leave the tab order, F6's cycle and hit-testing, so focus
    // never lands where it can't be seen (WCAG 2.4.3, 2.4.7). The sheet hides the page's disabled look. The page is
    // enabled again as soon as the sheet closes or stops covering, before focus returns to it (KeepFocus runs later).
    // KeepFocusOrReannounce (ShellWindow.Focus.cs) also pulses Page.IsEnabled to drop and retake focus, but that is on the Page
    // border; this is PagePanel, the page's container, so the two don't fight.
    private void CoverPage()
    {
        PagePanel.IsEnabled = !(Covering && Shell is { IsSheetShown: true });
        ReservePage();
    }

    // Beside the page the sheet takes its width from the page region: the page is the window minus the sheet, so nothing the
    // keyboard reaches is under it (WCAG 2.4.11, proof B8), and the page's rows and Home adapt to the narrower width
    // (PageLayout). The width is the sheet's clamped one, not the requested.
    private void ReservePage() =>
        PagePanel.Margin = new Thickness(0, 0, !Covering && Shell is { IsSheetShown: true } && !double.IsNaN(SheetPanel.Width) ? ClampedSheetWidth() : 0, 0);

    private double ClampedSheetWidth() => Math.Clamp(SheetPanel.Width, SheetPanel.MinWidth, SheetPanel.MaxWidth);

    private bool Covering => SheetPanel.Classes.Contains("covers");

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.SheetWidth) or nameof(ShellViewModel.ShownSheetWidth):
                Cover(Bounds.Width);
                break;
            case nameof(ShellViewModel.State):
                CoverPage();
                Dispatcher.UIThread.Post(Shell is { IsVersionMismatch: true } ? FocusMismatchCard : KeepFocus, DispatcherPriority.Loaded);
                break;
            case nameof(ShellViewModel.CurrentPage):
                OnPageChanged();
                break;
            case nameof(ShellViewModel.IsNeedsYouInbox):
                FollowInbox();
                break;
            case nameof(ShellViewModel.IsSheetShown):
                CoverPage();
                Dispatcher.UIThread.Post(Shell is { IsSheetShown: true } ? FocusOpenedSheet : KeepFocus, DispatcherPriority.Loaded);
                break;
            default:
                break;
        }
    }

    // A page switch (a G-chord) hides the page the focused control was on: focus goes to the page region, which is named, so the new page is spoken.
    // Arrowing along the nav tabs keeps focus on the tab (Y1); every other page change lands on the region.
    private void OnPageChanged()
    {
        if (_openingSettings)
        {
            return;
        }

        var arrowing = _navArrowing;
        _navArrowing = false;
        if (arrowing)
        {
            Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);   // arrows keep focus on the tab (Y1)
        }
        else
        {
            LandOnPage();   // the page was opened (click, chord, link, relaunch), not arrowed onto
        }
    }

    // The version-mismatch card replaces the whole shell (it can appear mid-session), so whatever had focus is hidden and
    // there is no neighbour: focus goes into the card, to its heading, which is spoken with the card's live region
    // (review run 5, accessibility-tester-0).
    private void FocusMismatchCard()
    {
        if (Shell is { IsVersionMismatch: true })
        {
            _ = this.Get<TextBlock>("VersionMismatchText").Focus(Method());
        }
    }

    // A keyboard user's focus moves with its ring (Tab); a pointer user's without.
    private NavigationMethod Method() => _lastMethod == NavigationMethod.Pointer ? NavigationMethod.Pointer : NavigationMethod.Tab;

    private void OnSheetDrag(object? sender, VectorEventArgs e)
    {
        SheetPanel.Width = Math.Clamp(SheetPanel.Bounds.Width - e.Vector.X, SheetPanel.MinWidth, SheetPanel.MaxWidth);
        ReservePage();
    }

    private void OnSheetDragged(object? sender, VectorEventArgs e) => Shell?.ResizeSheet(SheetPanel.Bounds.Width);

    private void OnCloseSheet(object? sender, RoutedEventArgs e) => Shell?.CloseSheet();

    private void OnEdgeTab(object? sender, RoutedEventArgs e)
    {
        _sheetOpener = this.Get<Button>("EdgeTab");
        Shell?.ReopenSheet();
    }

    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is Control control)
        {
            _lastFocused = control;
            _lastMethod = e.NavigationMethod;
        }
    }
}
