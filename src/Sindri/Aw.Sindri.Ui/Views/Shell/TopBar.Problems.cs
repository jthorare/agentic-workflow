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

    // The pill without its problem text (the Refresh button, the glyph, the word, the age while shown), from the last
    // whole measure of the bar, so every part of it comes from one layout (#2547: the group's width from one pass less
    // the problem text's from the next swung the room by 180 px, and the bar never settled).
    private double _pillRest;

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
        if (double.IsInfinity(width) || Content is not Border bar)
        {
            SetProblemsCap(ProblemsMax);
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

        var others = bar.Padding.Left + bar.Padding.Right + Brand.DesiredSize.Width + NavigationWidthAt(_fit) + search + _pillRest;
        var room = width - others - PillProblems.Margin.Left - PillProblems.Margin.Right;
        SetProblemsCap(Math.Clamp(YieldAge(room), 0, ProblemsMax));
    }

    // A cap that moves by less than half a pixel stays: every new value measures the bar again.
    private void SetProblemsCap(double cap)
    {
        if (Math.Abs(PillProblems.MaxWidth - cap) > 0.5)
        {
            PillProblems.MaxWidth = cap;
        }
    }

    // Remembered after a whole measure: the pill's width less its problem text and that text's margin.
    private void RememberPillRest()
    {
        var problems = PillProblems.IsVisible ? PillProblems.DesiredSize.Width : 0;
        _pillRest = Math.Max(0, PillGroup.DesiredSize.Width - problems);
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
}
