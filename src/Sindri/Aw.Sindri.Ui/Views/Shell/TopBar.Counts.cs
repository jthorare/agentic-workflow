using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The top bar (continued): the navigation's counts, the last thing to give way at 1,000 px (UI spec §3.2).</summary>
internal sealed partial class TopBar
{
    // What the shown counts take, remembered while they show so the bar knows what hiding them would give back.
    private double CountsWidth { get; set; }

    private ListBox NavigationList => this.Get<ListBox>("Navigation");

    private IEnumerable<TextBlock> CountBlocks => NavigationList.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("nav-count"));

    private void RememberCounts()
    {
        if (_fit < Fit.NoCounts)
        {
            CountsWidth = CountBlocks.Where(t => t.IsVisible).Sum(t => t.DesiredSize.Width + SpacingBeforeCount);
        }
    }

    // The navigation's width with its counts and its full tab padding, which is what every step measures from.
    private double NavigationWidthWithCounts => NavSegments.DesiredSize.Width + (_fit == Fit.NoCounts ? CountsWidth : 0) + (_fit >= Fit.TightNav ? TightSaving : 0);

    // What the tight step gives back: the narrower padding on both sides of every tab, and the gaps between the tabs
    // (#2547: with full labels at every width, nine tabs at 130% text still ran 7 px past a 1,000 px window).
    private double TightSaving => (NavigationList.ItemCount * 2 * (TabPadding - TightTabPadding)) + (Math.Max(NavigationList.ItemCount - 1, 0) * (NormalTabGap - TightTabGap));

    // The tight step closes the gaps between the tabs (the items panel, TopBar.axaml); the segment's hover and selected fills still have their padding.
    private void ApplyTabGap(Fit fit)
    {
        if (NavigationList.ItemsPanelRoot is StackPanel tabs)
        {
            var normal = NormalTabGap;   // read before the first change: the markup's Spacing
            tabs.Spacing = fit >= Fit.TightNav ? TightTabGap : normal;
        }
    }

    // The tabs' horizontal padding (ShellStyles: ListBox.nav > ListBoxItem) and the tight step's.
    private const double TabPadding = 7;
    private const double TightTabPadding = 2;

    // The gap between two tabs is the items panel's Spacing in TopBar.axaml, read once; the tight step's is none.
    private const double TightTabGap = 0;

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

    // The count sits after its label in a stack spaced by 6.
    private const double SpacingBeforeCount = 6;
}
