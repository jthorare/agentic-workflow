namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>The Create item form made an item (UI spec §4.5).</summary>
/// <param name="itemId">The new item's id.</param>
internal sealed class ItemCreatedEventArgs(string itemId) : EventArgs
{
    /// <summary>The new item's id.</summary>
    public string ItemId { get; } = itemId;
}
