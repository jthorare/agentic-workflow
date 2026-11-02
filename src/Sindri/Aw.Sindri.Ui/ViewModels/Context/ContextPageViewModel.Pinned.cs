namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Opening the page for an item (<see cref="ContextPageViewModel"/>, continued): the item's repo is read, its frozen starting points fill the box when it has any, always against the live map; the page says which item it is for, while it loads, and why a read failed.</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>What the page is doing for the item it was opened on (loading, not scoped yet, or why it failed); empty otherwise.</summary>
    public string ItemNote { get; private set; } = string.Empty;

    /// <summary>Whether the item note shows.</summary>
    public bool HasItemNote => ItemNote.Length > 0;

    /// <summary>Whether the last open for an item failed, so Retry shows.</summary>
    public bool IsItemFailed { get; private set; }

    /// <summary>The retry button's words.</summary>
    public string ItemRetryText { get; } = Labels.ContextWholeRetry;

    private string? _itemOpened;

    /// <summary>Opens the page for an item: shows that it is reading, opens the item's repo, and fills the starting points from its frozen scope, or says there is none.</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the scope is read and the live map is drawn.</returns>
    internal async Task OpenForItem(string itemId, CancellationToken cancellationToken = default)
    {
        _itemOpened = itemId;
        if (Ports.ApiUrl() is not { } api)
        {
            NoteItem(Labels.ContextItemFailed(itemId, Labels.ContextItemUnreachable), failed: true);
            return;
        }

        NoteItem(Labels.ContextItemLoading(itemId));
        // The repo is a courtesy: an item that can't be read here still gets its note and the map as it is.
        var item = await Ports.Api.Item(api, itemId, cancellationToken).ConfigureAwait(true);
        var repo = item.IsSuccess ? item.Value.Repo : null;
        if (repo is not null)
        {
            await PickRepo(repo, cancellationToken).ConfigureAwait(true);
        }

        var scope = await Ports.Api.ItemScope(api, itemId, cancellationToken).ConfigureAwait(true);
        if (!scope.IsSuccess)
        {
            NoteItem(Labels.ContextItemFailed(itemId, scope.Error.Message), failed: true);
            return;
        }

        if (scope.Value.Scope is not { } frozen)
        {
            NoteItem(Labels.ContextItemUnscoped(itemId, repo));
            return;
        }

        AnchorText = string.Join(' ', frozen.Anchors.Select(a => a.Key).Distinct(StringComparer.Ordinal));
        await Submit(cancellationToken).ConfigureAwait(true);
        NoteItem(Labels.ContextItemScoped(itemId));
    }

    /// <summary>Retry on the item note: the open is made again.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal Task RetryItem(CancellationToken cancellationToken = default) =>
        _itemOpened is { } item ? OpenForItem(item, cancellationToken) : Task.CompletedTask;

    private void NoteItem(string note, bool failed = false)
    {
        ItemNote = note;
        IsItemFailed = failed;
        OnChanged(nameof(IsItemFailed));
        OnChanged(nameof(ItemNote));
        OnChanged(nameof(HasItemNote));
    }
}
