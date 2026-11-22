using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Opening the page for an item (<see cref="ContextPageViewModel"/>, continued): the item is read and its repo map opens; the page says which item it is for, while it loads, that it is not scoped yet, and why a read failed.</summary>
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

    /// <summary>Opens the page for an item: shows that it is reading, opens the item's repo, and says the item is not scoped yet (no route serves starting points for an item).</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the item's repo map is drawn and the note is set.</returns>
    internal async Task OpenForItem(string itemId, CancellationToken cancellationToken = default)
    {
        ItemOpened(itemId);
        if (Ports.ApiUrl() is not { } api)
        {
            NoteItem(Labels.ContextItemFailed(itemId, Labels.ContextItemUnreachable), failed: true);
            return;
        }

        NoteItem(Labels.ContextItemLoading(itemId));
        var item = await Ports.Api.Item(api, itemId, cancellationToken).ConfigureAwait(true);
        if (!item.IsSuccess)
        {
            // The error's message names the route and the status, so the note never carries it.
            var (note, failed) = ItemReadFailureNote(itemId, item.Error);
            NoteItem(note, failed);
            return;
        }

        var repo = item.Value.Repo;
        if (repo is not null)
        {
            await PickRepo(repo, cancellationToken).ConfigureAwait(true);
        }

        // Starting points for an item are a future feature: no route serves them, so the page says it is not scoped yet.
        NoteItem(Labels.ContextItemUnscoped(itemId, repo));
    }

    // Only an error the client made because no answer came says "not reachable"; an id Sindri refused or doesn't know reads as unknown; any other answered error says Sindri couldn't read it.
    private const string ClientCodePrefix = "SND-UI-";

    private static (string Note, bool Failed) ItemReadFailureNote(string itemId, Error error)
    {
        if (string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal))
        {
            return (Labels.ContextItemFailed(itemId, Labels.ContextItemUnreachable), true);
        }

        // why: the client's own SND-UI-003 for a body this build can't parse is Kind Invalid too, and it is not Sindri refusing the id
        var answeredInvalid = (error.Kind is ErrorKind.NotFound or ErrorKind.Invalid) && !error.Code.StartsWith(ClientCodePrefix, StringComparison.Ordinal);
        if (answeredInvalid || error.IsNotFound() || error.IsBadRequest())
        {
            return (Labels.ContextItemUnknown(itemId), false);
        }

        return (Labels.ContextItemFailed(itemId, Labels.ContextItemUnreadable), true);
    }

    /// <summary>Retry on the item note: the open is made again.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal Task RetryItem(CancellationToken cancellationToken = default) =>
        _itemOpened is { } item ? OpenForItem(item, cancellationToken) : Task.CompletedTask;

    // The item the page is open for, or null on the bare page: the No map state's words follow it.
    private void ItemOpened(string? itemId)
    {
        _itemOpened = itemId;
        OnChanged(nameof(EmptyTitle));
        OnChanged(nameof(EmptyBody));
        OnChanged(nameof(BuildingTitle));
        OnChanged(nameof(BuildingBody));
        OnChanged(nameof(HasBuildingBody));
        OnChanged(nameof(BuildingName));
    }

    private void NoteItem(string note, bool failed = false)
    {
        ItemNote = note;
        IsItemFailed = failed;
        OnChanged(nameof(IsItemFailed));
        OnChanged(nameof(ItemNote));
        OnChanged(nameof(HasItemNote));
    }
}
