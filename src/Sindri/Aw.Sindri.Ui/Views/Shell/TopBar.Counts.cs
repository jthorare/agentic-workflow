using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The top bar (continued): what the navigation gives way at 1,000 px, its dot, its counts and its tab padding (UI spec §3.2).</summary>
internal sealed partial class TopBar
{
    // What the shown counts take, remembered while they show so the bar knows what hiding them would give back.
    internal double CountsWidth { get; private set; }

    // What the selected tab's dot takes, remembered while it shows.
    internal double DotWidth { get; private set; }

    private ListBox NavigationList => this.Get<ListBox>("Navigation");

    private IEnumerable<T> NavParts<T>(string name)
        where T : Control => NavigationList.GetVisualDescendants().OfType<T>().Where(t => t.Classes.Contains(name) && t.IsVisible);

    private void RememberCounts()
    {
        if (_fit < Fit.NoCounts)
        {
            CountsWidth = NavParts<TextBlock>("nav-count").Sum(t => t.DesiredSize.Width + SpacingBefore(t));
        }

        if (_fit < Fit.NoDot)
        {
            DotWidth = NavParts<Control>("nav-dot").Sum(d => d.DesiredSize.Width + SpacingBefore(d));
        }
    }

    // A tab's parts sit in a stack (TopBar.axaml): hiding one gives back its width and one Spacing (the one before it, or
    // after it for the first part, the dot).
    private static double SpacingBefore(Control part) => part.Parent is StackPanel stack ? stack.Spacing : 0;

    // The navigation's width at the full step (its dot, its counts, its full tab padding): what every step measures from.
    internal double NavigationWidthFull => NavSegments.DesiredSize.Width + Given(_fit);

    // The navigation's width at a step.
    private double NavigationWidthAt(Fit fit) => NavigationWidthFull - Given(fit);

    // What a step has taken off the navigation.
    private double Given(Fit fit) => (fit >= Fit.NoDot ? DotWidth : 0) + (fit >= Fit.NoCounts ? CountsWidth : 0) + (fit >= Fit.TightNav ? TightSaving : 0);

    // What the tight step gives back: the narrower padding on both sides of every tab, and the gaps between the tabs
    // (#2547: with full labels at every width, nine tabs at 130% text still ran past a 1,000 px window).
    private double TightSaving => (NavigationList.ItemCount * 2 * (TabPadding - TightTabPadding)) + (Math.Max(NavigationList.ItemCount - 1, 0) * (NormalTabGap - TightTabGap));

    // The tight step sets the gap between the tabs (the items panel, TopBar.axaml); the segment's hover and selected fills keep their padding.
    private void ApplyTabGap(Fit fit)
    {
        if (NavigationList.ItemsPanelRoot is StackPanel tabs)
        {
            var normal = NormalTabGap;   // read before the first change: the markup's Spacing
            tabs.Spacing = fit >= Fit.TightNav ? TightTabGap : normal;
        }
    }

    // The tabs' horizontal padding (ShellStyles: ListBox.nav > ListBoxItem) and the tight step's. With the tight gap below:
    // 3 + 3 + 3 = 9 px between two tabs' words (QA's floor 8), and 3 + 3 = 6 px from a selected fill's edge to the next
    // tab's word, so the fill never reads as part of that word (#2547).
    private const double TabPadding = 7;
    private const double TightTabPadding = 3;

    // The gap between two tabs is the items panel's Spacing in TopBar.axaml, read once; the tight step's is 3 (above).
    private const double TightTabGap = 3;

    private double NormalTabGap
    {
        get
        {
            if (double.IsNaN(field) && NavigationList.ItemsPanelRoot is StackPanel tabs)
            {
                field = tabs.Spacing;
            }

            return double.IsNaN(field) ? 0 : field;
        }
    } = double.NaN;
}
