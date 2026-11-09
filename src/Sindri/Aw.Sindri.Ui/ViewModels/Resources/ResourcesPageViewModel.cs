using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Resources;

namespace Aw.Sindri.Ui.ViewModels.Resources;

/// <summary>
/// The Resources page (PR 18b): what each owner holds (kind, name, size) and a Purge per owner. A purge is refused while the owner is active; the page shows the
/// refusal's reason and keeps the rows. After a purge that worked, the list is read again.
/// </summary>
internal sealed class ResourcesPageViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly HashSet<string> _purging = new(StringComparer.Ordinal);
    private Uri? _url;

    /// <summary>The page over its port.</summary>
    /// <param name="api">The API.</param>
    internal ResourcesPageViewModel(ISindriApi api)
    {
        _api = api;
    }

    /// <summary>The resources, as the API orders them.</summary>
    public ObservableCollection<ResourceRowViewModel> Rows { get; } = [];

    /// <summary>True when the list is empty.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>The sentence for an empty list.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>What the last purge said: its refusal's reason, or how many went; or why a read failed; empty before any.</summary>
    public string NoteText
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasNote));
        }
    } = string.Empty;

    /// <summary>True when the first read failed and there are no rows to show.</summary>
    public bool IsFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when a retry is offered.</summary>
    public bool ShowRetry { get; private set => Set(ref field, value); }

    /// <summary>Reads again after a failed read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the list is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) => _url is { } url ? Refresh(url, cancellationToken) : Task.CompletedTask;

    /// <summary>True when there is a note.</summary>
    public bool HasNote => NoteText.Length > 0;

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The page's name.</summary>
    public string Title => Labels.ResourcesPage;

    /// <summary>What the page is for, in one line.</summary>
    public string Purpose => Labels.ResourcesPurpose;

    /// <summary>The Try again button's words.</summary>
    public string RetryText => Labels.TryAgain;

    /// <summary>"Reading resources…" until the first answer.</summary>
    public string ReadingText => Labels.ReadingResources;
#pragma warning restore CA1822, MA0041

    /// <summary>"3 resources"; empty until the first list.</summary>
    public string Head { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True until the first answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); } = true;

    /// <summary>True while the page is the one shown.</summary>
    internal bool IsActive { get; private set; }

    /// <summary>The read made when the page was shown.</summary>
    internal Task CatchUp { get; private set; } = Task.CompletedTask;

    /// <summary>The control plane connected or an event came: remembers where the API is, and reads only while the page is shown.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the list is shown.</returns>
    internal Task Connected(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return IsActive ? Refresh(url, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>The page was shown or hidden. Becoming the page shown reads at once.</summary>
    /// <param name="shown">True while Resources is the page.</param>
    internal void SetShown(bool shown)
    {
        var was = IsActive;
        IsActive = shown;
        if (shown && !was && _url is { } url)
        {
            CatchUp = Refresh(url, CancellationToken.None);
        }
    }

    /// <summary>Reads the list: one row per resource.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the list is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        var read = await _api.ListResources(url, owner: null, cancellationToken).ConfigureAwait(true);
        IsReading = false;
        if (read.IsError)
        {
            // the last good rows stay under the failed text; an earlier purge's "Purged 2" is not left standing over them
            (IsFailed, FailedText, ShowRetry, NoteText) = (Rows.Count == 0, Labels.ResourcesFailed(read.Error), true, string.Empty);
            return;
        }

        (IsFailed, FailedText, ShowRetry) = (false, string.Empty, false);

        Rows.Clear();
        foreach (var resource in read.Value.Resources)
        {
            Rows.Add(new ResourceRowViewModel(resource, Purge));
        }

        IsEmpty = Rows.Count == 0;
        Head = Labels.ResourcesHead(Rows.Count);
        EmptyText = IsEmpty ? Labels.ResourcesEmpty : string.Empty;
    }

    // A purge: the API's refusal keeps the rows and shows its detail; a purge that worked reads the list again.
    private async Task Purge(string owner, CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return;
        }

        if (!_purging.Add(owner))
        {
            return;   // this owner's purge is already on its way
        }

        NoteText = string.Empty;
        Result<PurgeResourcesResult> purged;
        try
        {
            purged = await _api.PurgeResources(url, owner, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _ = _purging.Remove(owner);
        }

        if (purged.IsError)
        {
            NoteText = Labels.ResourcePurgeFailed(purged.Error);
            return;
        }

        NoteText = Labels.ResourcePurged(owner, purged.Value.Removed);
        await Refresh(url, cancellationToken).ConfigureAwait(true);
    }
}
