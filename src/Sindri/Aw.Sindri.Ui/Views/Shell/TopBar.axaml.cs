using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// The shell's top bar: binds to the shell, and passes the health pill's click to it. It also fits itself to the window
/// (§3.2, minimum 1,000 px at text scale up to 1.3): with room the ⌘K field is 160–300 px wide. When the parts don't fit,
/// they give way in this order: the field folds to its magnifier, the pill's age drops (the pill's accessible name keeps
/// it), the selected tab's dot, the tabs' counts, the brand's word (the window and the bar's region are still named
/// Sindri), the tabs' padding narrows to 3 px a side (#2547: never under QA's 6 px between two tabs' words), and last
/// Refresh keeps only its icon (its name and tooltip still say Refresh). The pill's word always shows. This is layout
/// only; the shell decides what the words are.
/// </summary>
internal sealed partial class TopBar : UserControl
{
    // The ⌘K field's column with room: a 100* star column of 184–324 px (the field's 160–300 px and its 12 px margins).
    private const double SearchMin = 184;
    private const double SearchMax = 324;

    // Growing back takes this much more room than shrinking did, so a ticking age ("9s" to "10s") can't make the bar flicker.
    private const double Slack = 8;

    // The steps, widest first.
    private static readonly Fit[] _steps = Enum.GetValues<Fit>();

    private Fit _fit = Fit.Full;
    private double _ageWidth;
    private double _brandWordWidth;
    private double _refreshWordWidth;
    private double _compactSearch = 60;

    // The keycap's own width, seen while it was shown. It sits in an Auto column, so it does not depend on the field's column;
    // it is hidden while folded, so SearchChrome uses the last width seen.
    private double _keycapWidth;

    // Refresh had focus when it was disabled (in flight) or hidden (recovered); see KeepRefreshFocus.
    private bool _refreshHeldFocus;
    private NavigationMethod _refreshMethod = NavigationMethod.Tab;

    /// <summary>How many times the bar was measured since the last reset; a bar that never settles measures again and again.</summary>
    internal int MeasurePasses { get; private set; }

    /// <summary>Sets <see cref="MeasurePasses"/> back to 0.</summary>
    internal void ResetMeasurePasses() => MeasurePasses = 0;

    public TopBar()
    {
        InitializeComponent();
        RefreshButton.GotFocus += OnRefreshGotFocus;
        RefreshButton.LostFocus += OnRefreshLostFocus;
        RefreshButton.PropertyChanged += OnRefreshChanged;
    }

    // How far the bar has stepped down.
    private enum Fit
    {
        Full,
        CompactSearch,
        NoAge,
        NoDot,
        NoCounts,
        NoBrandWord,
        TightNav,
        NoRefreshWord,
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        MeasurePasses++;
        CapProblems(availableSize.Width);
        var size = MeasureParts(availableSize);
        Remember();
        var fit = Choose(availableSize.Width);
        if (fit != _fit)
        {
            Apply(fit);
            size = MeasureParts(availableSize);
            Remember();
        }

        return size;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // A part can grow after the bar was measured (a text-size change reaches it late); measure again to step down.
        // Only a real change measures again: the step, the problem text's cap (by more than half a pixel), or the age.
        Remember();
        var cap = PillProblems.MaxWidth;
        var yielded = _ageYields;
        CapProblems(finalSize.Width);
        if (Choose(finalSize.Width) != _fit || Math.Abs(PillProblems.MaxWidth - cap) > 0.5 || yielded != _ageYields)
        {
            InvalidateMeasure();
        }

        return base.ArrangeOverride(finalSize);
    }

    // The widest step that fits: the bar's padding and the brand's mark are fixed; then the navigation at the step, the
    // ⌘K field (full or folded), and the pill without its problem text (which takes only what is left, TopBar.Problems.cs),
    // with its age and Refresh's word as the step has them (measured while shown, remembered while dropped).
    private Fit Choose(double width)
    {
        if (double.IsInfinity(width))
        {
            return Fit.Full;
        }

        var parts = new StepWidths(FixedWidth(), _brandWordWidth + Brand.Spacing, _compactSearch, FullSearch(), PillWithoutProblems(),
            AgeWidth(), RefreshButton.IsVisible ? _refreshWordWidth : 0);
        foreach (var step in _steps)
        {
            if (parts.Need(step, NavigationWidthAt(step)) <= width - (step < _fit ? Slack : 0))
            {
                return step;
            }
        }

        return Fit.NoRefreshWord;
    }

    // The age with the pill's spacing before it, when the pill shows one and it hasn't yielded to the problem text.
    private double AgeWidth() =>
        PillAgeText.Text is { Length: > 0 } && !_ageYields ? _ageWidth + PillGroup.Spacing : 0;

    // The pill without its problem text, age and Refresh's word, whatever the current step shows of them. The problem text's
    // margin stays in: it shows even when the text's cap is 0, so a step that leaves it no room overruns the bar (#2547).
    private double PillWithoutProblems()
    {
        var age = PillAge.IsVisible ? AgeWidth() : 0;
        var refresh = RefreshButton.IsVisible && _fit >= Fit.NoRefreshWord ? _refreshWordWidth : 0;
        var margin = PillProblems.IsVisible ? PillProblems.Margin.Left + PillProblems.Margin.Right : 0;
        return _pillRest - age + refresh + margin;
    }

    // The bar's padding and the brand without its word.
    private double FixedWidth()
    {
        var padding = Content is Border bar ? bar.Padding.Left + bar.Padding.Right : 0;
        return padding + Brand.DesiredSize.Width - (BrandWord.IsVisible ? _brandWordWidth + Brand.Spacing : 0);
    }

    // The ⌘K field's column (the fifth).
    private ColumnDefinition SearchColumn => BarGrid.ColumnDefinitions[4];

    private void Apply(Fit fit)
    {
        _fit = fit;
        var compact = fit != Fit.Full;
        SearchField.Classes.Set("compact", compact);
        SearchColumn.Width = compact ? GridLength.Auto : new GridLength(100, GridUnitType.Star);
        SearchColumn.MinWidth = compact ? 0 : FullSearch();
        SearchColumn.MaxWidth = compact ? double.PositiveInfinity : SearchMax;
        PillAge.IsVisible = fit < Fit.NoAge && !_ageYields;
        NavigationList.Classes.Set("no-dot", fit >= Fit.NoDot);
        NavigationList.Classes.Set("no-counts", fit >= Fit.NoCounts);
        BrandWord.IsVisible = fit < Fit.NoBrandWord;
        NavigationList.Classes.Set("tight", fit >= Fit.TightNav);
        ApplyTabGap(fit);
        RefreshButton.Classes.Set("folded", fit >= Fit.NoRefreshWord);
    }

    // The widths a dropped part would need again.
    private void Remember()
    {
        if (PillAge.IsVisible && PillAge.DesiredSize.Width > 0)
        {
            _ageWidth = PillAge.DesiredSize.Width;
        }

        if (BrandWord.IsVisible && BrandWord.DesiredSize.Width > 0)
        {
            _brandWordWidth = BrandWord.DesiredSize.Width;
        }

        if (RefreshWord.IsVisible && RefreshButton.IsVisible && RefreshWord.DesiredSize.Width > 0)
        {
            _refreshWordWidth = RefreshWord.DesiredSize.Width + SpacingBefore(RefreshWord);
        }

        RememberCounts();
        RememberPillRest();
        RememberSearch();
    }

    // The folded field's width, and the keycap's width while it shows (SearchChrome). The field's own size can't measure its
    // chrome while Full: its inner grid has a star column, so the field desires the whole column it is given.
    private void RememberSearch()
    {
        if (SearchKeycap.IsVisible && SearchKeycap.DesiredSize.Width > 0)
        {
            _keycapWidth = SearchKeycap.DesiredSize.Width;
        }

        if (_fit != Fit.Full && SearchField.DesiredSize.Width > 0)
        {
            _compactSearch = SearchField.DesiredSize.Width;
        }
    }

    // Everything in the ⌘K field except the placeholder's text, summed from parts whose size doesn't depend on the column:
    // margin, border and padding, the magnifier, the keycap and the two gaps between the three cells.
    private double SearchChrome() =>
        SearchField.Margin.Left + SearchField.Margin.Right
        + SearchField.BorderThickness.Left + SearchField.BorderThickness.Right
        + SearchField.Padding.Left + SearchField.Padding.Right
        + SearchMagnifier.Width + (2 * SearchCells.ColumnSpacing) + _keycapWidth;

    private void OnRefreshGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _refreshHeldFocus = true;
        _refreshMethod = e.NavigationMethod;
    }

    // Focus that leaves a usable Refresh went somewhere on purpose; focus taken by disabling or hiding it comes back.
    private void OnRefreshLostFocus(object? sender, FocusChangedEventArgs e) =>
        _refreshHeldFocus &= !(RefreshButton.IsEffectivelyVisible && RefreshButton.IsEffectivelyEnabled);

    private void OnRefreshChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_refreshHeldFocus && (e.Property == IsVisibleProperty || e.Property == IsEnabledProperty))
        {
            Dispatcher.UIThread.Post(KeepRefreshFocus, DispatcherPriority.Loaded);
        }
    }

    // §4.1, §11.2: a refresh that fails leaves focus on Refresh (it was only disabled while in flight); one that
    // recovers hides Refresh, and focus moves to its neighbour, the pill. Focus the user moved elsewhere stays there.
    private void KeepRefreshFocus()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        if (!_refreshHeldFocus || (focused is { IsEffectivelyVisible: true } && focused != RefreshButton))
        {
            _refreshHeldFocus = false;
            return;
        }

        if (!RefreshButton.IsEffectivelyVisible)
        {
            _refreshHeldFocus = false;
            _ = HealthPill.Focus(_refreshMethod);
        }
        else if (RefreshButton.IsEffectivelyEnabled && focused != RefreshButton)
        {
            _ = RefreshButton.Focus(_refreshMethod);
        }
    }

    // The pill opens Health. While Sindri isn't running it still does: the Not running card covers the page, and the selected tab moves to System (#3688).
    private void OnHealthPill(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel { IsNavigationEnabled: true } shell)
        {
            shell.GoTo(PageId.System);
        }
    }

    // Refresh re-reads the status (a query); the pill shows the answer when it comes.
    private void OnRefresh(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell)
        {
            _ = shell.Health.Refresh(CancellationToken.None);
        }
    }
    // The ⌘K field opens the palette (§8): a click, or Enter or Space while it has focus.
    private void OnSearch(object? sender, PointerPressedEventArgs e)
    {
        (DataContext as ShellViewModel)?.Palette.Open();
        e.Handled = true;
    }

    private void OnSearchKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && DataContext is ShellViewModel shell)
        {
            shell.Palette.Open();
            e.Handled = true;
        }
    }

    // What a step needs, from the parts that change between steps (Choose).
    private sealed record StepWidths(
        double Fixed, double BrandWord, double CompactSearch, double FullSearch, double Pill, double Age, double RefreshWord)
    {
        public double Need(Fit step, double navigation) =>
            Fixed + navigation + (step < Fit.NoBrandWord ? BrandWord : 0) + (step == Fit.Full ? FullSearch : CompactSearch)
            + Pill + (step < Fit.NoAge ? Age : 0) - (step >= Fit.NoRefreshWord ? RefreshWord : 0);
    }
}
