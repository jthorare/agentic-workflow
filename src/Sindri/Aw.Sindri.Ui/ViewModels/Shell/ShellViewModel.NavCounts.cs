using System.Globalization;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Home;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The top bar's counts (<see cref="ShellViewModel"/>, continued; UI spec §3.2, #4227): the one place that writes a tab's count. With Home's
/// panels a tab shows its own page's count. With Home as rows, and once the rows are read, Home, Reviews, Work and Agents show their Home tab's
/// count, so the bar and Home's tabs say the same number on one screen.
/// </summary>
internal sealed partial class ShellViewModel
{
    // Each page's own count, kept while Home's rows own the bar, so the panels get it back at once.
    private static readonly Dictionary<PageId, HomeSplit> _homeSplits = new()
    {
        [PageId.Home] = HomeSplit.All,
        [PageId.Reviews] = HomeSplit.Reviews,
        [PageId.Work] = HomeSplit.Work,
        [PageId.Agents] = HomeSplit.Agents,
    };

    private readonly Dictionary<PageId, string> _pageCounts = [];

    private bool _homeRowsRead;

    // A page's own count changed (the queue's total, the live sessions, the reviews waiting).
    private void ShowPageCount(PageId page, string count)
    {
        _pageCounts[page] = count;
        ShowNavCounts();
    }

    // Home's rows were read: from now on they are the source of the four counts while the rows preference is on.
    private void ShowHomeCounts()
    {
        _homeRowsRead = true;
        ShowNavCounts();
    }

    private void ShowNavCounts()
    {
        foreach (var entry in Navigation)
        {
            if (HomeSplitOf(entry.Page) is { } split)
            {
                entry.Count = IsHomeRows && _homeRowsRead ? HomeCount(split) : _pageCounts.GetValueOrDefault(entry.Page, string.Empty);
            }
        }
    }

    private string HomeCount(HomeSplit split) =>
        HomeRows.Tabs.FirstOrDefault(t => t.Split == split)?.CountText ?? 0.ToString(CultureInfo.InvariantCulture);

    // The Home tab a top bar tab counts with; a page with no Home tab (Projects, Workflows, ..) has no count of this kind.
    private static HomeSplit? HomeSplitOf(PageId page) => _homeSplits.TryGetValue(page, out var split) ? split : null;
}
