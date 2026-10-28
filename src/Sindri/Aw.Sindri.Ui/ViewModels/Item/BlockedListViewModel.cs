using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>The item sheet's Blocked list: what the sandbox denied the item's launches, with a fix per row (SB-6b).</summary>
internal sealed class BlockedListViewModel(ISindriApi api) : ObservableObject
{
    /// <summary>The list's heading.</summary>
    public string Heading { get; } = Labels.BlockedHeading;

    /// <summary>"Nothing was blocked" or "Blocked: top 5 of 12"; empty for a short list.</summary>
    public string Summary { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The rows shown: every target of a short list, the top five of a long one.</summary>
    public IReadOnlyList<BlockedRowViewModel> Rows { get; private set => Set(ref field, value); } = [];

    /// <summary>True when a long list shows only its top five.</summary>
    public bool HasSeeAll { get; private set => Set(ref field, value); }

    /// <summary>"See all blocked".</summary>
    public string SeeAllText { get; } = Labels.SeeAllBlocked;

    /// <summary>The one polite live region of the list and its Allow buttons.</summary>
    public LiveAnnouncer Voice { get; } = new(Labels.AllowResting);

    /// <summary>Raised when an Allow wants the owner to open the profile approval.</summary>
    internal event EventHandler<ApprovalRequestedEventArgs>? ApprovalRequested;

    /// <summary>Shows every target.</summary>
    internal void ShowAll() => OnChanged(nameof(Rows));

    /// <summary>Forgets the item (the sheet moved to another).</summary>
    internal void Reset() => OnChanged(nameof(Rows));

    /// <summary>Reads the item's denials.</summary>
    /// <param name="url">The API.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that ends when the list was drawn.</returns>
    internal async Task Read(Uri url, string itemId, CancellationToken cancellationToken)
    {
        _ = await api.SandboxDenials(url, itemId, cancellationToken).ConfigureAwait(true);
        ApprovalRequested?.Invoke(this, new ApprovalRequestedEventArgs(string.Empty, string.Empty));
    }
}
