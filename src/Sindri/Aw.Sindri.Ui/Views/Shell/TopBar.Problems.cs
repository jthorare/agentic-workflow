using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Aw.Sindri.Ui.Views.Shell;

// The pill's problem text gives way before the ⌘K field: it gets only what is left once the field has room for its
// whole placeholder (#1544), never more than its own 220 px and never less than nothing. The rest of the step-down
// (fold the field, drop the age, ...) is Choose's, over the narrower pill.
internal sealed partial class TopBar
{
    private const double ProblemsMax = 220;

    // The placeholder's target width is its text plus this: an exact fit trims on font rounding, and the pill's problem
    // text gives way instead.
    internal const double PlaceholderSlack = 12;

    // What an arrange found the placeholder short by, taken off the cap for this width; reset when the width changes.
    private double _loweredCap = double.PositiveInfinity;
    private double _loweredFor = double.NaN;

    // The age gives way before the problem text is trimmed (QA #2547): a short problem reads in full at 1280 px.
    private bool _ageYields;

    private double NaturalProblems()
    {
        if (!PillProblems.IsVisible || PillProblems.Text is not { Length: > 0 } text)
        {
            return 0;
        }

        using var layout = new TextLayout(text, new Typeface(PillProblems.FontFamily, PillProblems.FontStyle, PillProblems.FontWeight), PillProblems.FontSize, foreground: null);
        return Math.Min(ProblemsMax, Math.Ceiling(layout.Width) + 1);
    }

    private void CapProblems(double width)
    {
        if (width != _loweredFor)
        {
            _loweredCap = double.PositiveInfinity;
        }

        if (double.IsInfinity(width) || Content is not Border bar)
        {
            PillProblems.MaxWidth = ProblemsMax;
            return;
        }

        double search;
        if (_fit == Fit.Full)
        {
            SearchField.Measure(Size.Infinity);
            search = Math.Clamp(SearchField.DesiredSize.Width + PlaceholderSlack, SearchMin, SearchMax);
        }
        else
        {
            search = _compactSearch;
        }

        var problems = PillProblems.IsVisible ? PillProblems.DesiredSize.Width + PillProblems.Margin.Left + PillProblems.Margin.Right : 0;
        var others = bar.Padding.Left + bar.Padding.Right + Brand.DesiredSize.Width
            + NavigationWidthWithCounts + search + PillGroup.DesiredSize.Width - problems;
        var room = width - others - PillProblems.Margin.Left - PillProblems.Margin.Right;
        PillProblems.MaxWidth = Math.Min(Math.Clamp(YieldAge(room), 0, ProblemsMax), _loweredCap);
    }

    // Decides whether the age gives way to the problem text, applies it, and returns the room the problem text then has.
    private double YieldAge(double room)
    {
        var ageSize = PillAgeText.Text is { Length: > 0 } ? _ageWidth + PillGroup.Spacing : 0;
        var roomWithAge = room - (PillAge.IsVisible ? 0 : ageSize);
        var natural = NaturalProblems();
        _ageYields = natural > 0 && ageSize > 0 && natural > roomWithAge - (_ageYields ? Slack : 0);
        PillAge.IsVisible = _fit < Fit.NoAge && !_ageYields;
        return roomWithAge + (PillAge.IsVisible ? 0 : ageSize);
    }

    // After an arrange: if the placeholder is narrower than its text, the estimate above was short; take the shortfall
    // (and 1 px) off the problem text and measure again. It only ever lowers the cap, so it ends when the text fits or
    // the problem text is gone.
    private bool LowerCapByShortfall(double width)
    {
        if (!SearchText.IsEffectivelyVisible || !PillProblems.IsEffectivelyVisible || PillProblems.MaxWidth <= 0 || SearchText.Text is not { Length: > 0 } text
            || SearchColumn.ActualWidth >= SearchMax - 0.5)
        {
            return false;
        }

        using var layout = new TextLayout(text, new Typeface(SearchText.FontFamily, SearchText.FontStyle, SearchText.FontWeight), SearchText.FontSize, foreground: null);
        var shortfall = layout.Width + PlaceholderSlack - SearchText.Bounds.Width;
        if (shortfall <= 0.5)
        {
            return false;
        }

        _loweredFor = width;
        _loweredCap = Math.Max(0, Math.Min(PillProblems.MaxWidth, PillProblems.Bounds.Width) - shortfall - 1);
        return true;
    }
}
