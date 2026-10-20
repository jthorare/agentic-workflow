using System.ComponentModel;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's queue and item parts (<see cref="ShellViewModel"/>, continued; UI spec §4.5, §4.6, U2).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The queue (U2): the Queue page and Home's Running and Queued panels.</summary>
    public QueueViewModel Queue { get; }

    /// <summary>The item detail sheet (U2), shown when the sheet's subject is an item.</summary>
    public ItemSheetViewModel Item { get; }

    /// <summary>The Create item form (U2, `C`).</summary>
    public CreateItemViewModel Create { get; }

    /// <summary>True when the sheet's subject is an item (its own view in the sheet's content).</summary>
    public bool IsItemSheet => SheetLink?.Kind == DeepLinkKind.Item;

    /// <summary>Opens the Queue page on one filter (Home's "Show all N in Queue" on Running); every other way to Work shows the whole queue.</summary>
    /// <param name="filter">The filter.</param>
    internal void GoToQueue(QueueFilter filter)
    {
        Queue.Filter(filter);
        ShowPage(PageId.Work, HomeFilter.All);
    }

    /// <summary>Opens an item's sheet (a row's Enter or title click, §3.2).</summary>
    /// <param name="itemId">The item's id.</param>
    internal void OpenItem(string itemId)
    {
        Navigate(new DeepLink(DeepLinkKind.Item, itemId));
        OpenerRow = itemId;
    }

    /// <summary>The row the sheet was opened from (its item id), where Esc returns focus; null when the sheet came from elsewhere.</summary>
    internal string? OpenerRow { get; private set; }

    // §3.2: the Queue tab carries the queue's count from the view's rows.
    private void OnQueueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(QueueViewModel.Total) or nameof(QueueViewModel.IsLoading)))
        {
            return;
        }

        var count = Queue.IsLoading ? string.Empty : Queue.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Navigation.First(n => n.Page == PageId.Work).Count = count;
    }

    // The item sheet's part of opening the sheet (OpenSheet): the item is fetched; any other kind leaves it alone.
    private void OnSheetOpened(DeepLink link)
    {
        (OpenerRow, OpenerStrip) = (null, null);
        OnInstanceSheetOpened(link);
        OnRecipeSheetOpened(link);
        OnAgentSheetOpened(link);
        OnJobSheetOpened(link);
        if (link.Kind != DeepLinkKind.Item || link.Subject is null)
        {
            return;
        }

        Item.Show(link.Subject);
        if (Health.ApiUrl() is { } url)
        {
            _ = Item.Open(url, link.Subject, CancellationToken.None);
        }
    }
}
