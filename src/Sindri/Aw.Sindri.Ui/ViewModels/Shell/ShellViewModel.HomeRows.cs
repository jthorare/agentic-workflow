using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Home;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// Home as rows in the shell (<see cref="ShellViewModel"/>, continued; home spec §7.1, U1): one ranked list in place of Home's panels while the
/// preference asks for it (Settings' "Home as rows"). It scrolls itself, so it sits beside the pages' scroller as the inbox does. The list is read
/// on connect and after every event, every page of it, only while the preference is on.
/// </summary>
internal sealed partial class ShellViewModel
{
    // A read stops after this many pages: the API pages by 60, and a cursor that never ends must not hold the follower.
    private const int MaxHomePages = 40;

    /// <summary>Home's rows.</summary>
    public HomeRowsViewModel HomeRows { get; } = new();

    /// <summary>True when the preference shows Home as rows.</summary>
    public bool IsHomeRows { get; private set; }

    /// <summary>Home's rows show: Home with the rows preference, the page visible, and not the inbox over it.</summary>
    public bool IsHomeRowsShown => IsHome && IsHomeRows && !IsNeedsYouInbox && IsPageVisible;

    /// <summary>Shows Home as rows or as its panels (Settings' switch); rows are read at once when the API's address is known.</summary>
    /// <param name="rows">True for rows.</param>
    /// <returns>A task that completes when the rows are read; at once for the panels.</returns>
    internal async Task ShowHomeAsRows(bool rows)
    {
        if (IsHomeRows == rows)
        {
            return;
        }

        IsHomeRows = rows;
        Changed(nameof(IsHomeRows), nameof(IsHomeShown), nameof(IsHomeRowsShown), nameof(IsPagesShown));
        ShowNavCounts();
        if (rows && Health.ApiUrl() is { } url)
        {
            await ReadHomeRows(url, CancellationToken.None).ConfigureAwait(true);
        }
    }

    // Every page is read before any is shown, so the list never holds half of itself between two reads. A failed read leaves the rows as they
    // were; the next event reads again.
    private async Task ReadHomeRows(Uri url, CancellationToken cancellationToken)
    {
        if (!IsHomeRows)
        {
            return;
        }

        var pages = new List<HomeView>();
        string? after = null;
        do
        {
            var page = await _api.Home(url, after, cancellationToken).ConfigureAwait(true);
            if (page.IsError)
            {
                return;
            }

            pages.Add(page.Value);
            after = page.Value.Cursor;
        }
        while (after is not null && pages.Count < MaxHomePages);

        var now = _clock.GetUtcNow();
        HomeRows.Show(pages[0], now);
        foreach (var more in pages.Skip(1))
        {
            HomeRows.Add(more, now);
        }

        ShowHomeCounts();
    }
}
