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
    // text gives way instead. 4 px, not 12 (QA #2547): at 1280 px a short problem ("service broken failed") was 8 px short.
    internal const double PlaceholderSlack = 4;

    // The pill without its problem text (the Refresh button, the glyph, the word, the age while shown), from the last
    // whole measure of the bar, so every part of it comes from one layout (#2547: the group's width from one pass less
    // the problem text's from the next swung the room by 180 px, and the bar never settled).
    private double _pillRest;

    // The age gives way before the problem text is trimmed (QA #2547): a short problem reads in full at 1280 px.
    private bool _ageYields;

    // What Full reserves for the ⌘K field: the column the field needs to show its whole placeholder (#2710), between SearchMin
    // and SearchMax. "Full always means the whole placeholder". It must not read the field's size now: folded, the field's
    // placeholder and keycap are hidden and it measures as its magnifier. So it is two parts that hold in both steps: the
    // field's chrome (SearchChrome, summed from its fixed parts: the field's inner grid has a star column, so the field's own
    // size would grow with the column it is given and Full would grow its own column), and the placeholder's own width from a TextLayout on
    // SearchText's font, which follows a text-size change even while folded. The keycap's width is taken the next time it shows.
    private double FullSearch()
    {
        var text = SearchText.Text ?? string.Empty;
        using var layout = new TextLayout(text, new Typeface(SearchText.FontFamily, SearchText.FontStyle, SearchText.FontWeight), SearchText.FontSize, foreground: null);
        return Math.Clamp(SearchChrome() + Math.Ceiling(layout.Width) + PlaceholderSlack, SearchMin, SearchMax);
    }

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

        var search = _fit == Fit.Full ? FullSearch() : _compactSearch;

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
    // The group's own DesiredSize is clamped to its column when the bar is short of room, so it reads too small.
    // The group measures its children with unlimited width, so the sum of their widths is the pill's real width.
    private void RememberPillRest()
    {
        var problems = PillProblems.IsVisible ? PillProblems.DesiredSize.Width : 0;
        _pillRest = Math.Max(0, PillNaturalWidth() - problems);
    }

    private double PillNaturalWidth()
    {
        double total = 0;
        var visible = 0;
        foreach (var child in PillGroup.Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            total += child.DesiredSize.Width;
            visible++;
        }

        return total + (PillGroup.Spacing * Math.Max(0, visible - 1));
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
