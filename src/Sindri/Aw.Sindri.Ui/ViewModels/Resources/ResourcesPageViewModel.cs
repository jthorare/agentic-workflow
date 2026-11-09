using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Resources;

/// <summary>
/// The Resources page (PR 18b): what each owner holds (kind, name, size) and a Purge per owner. A purge is refused while the owner is active; the page shows the
/// refusal's reason and keeps the rows. After a purge that worked, the list is read again.
/// </summary>
#pragma warning disable MA0182 // the shell builds it in the views PR
internal sealed class ResourcesPageViewModel : ObservableObject
{
    private readonly ISindriApi _api;

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

    /// <summary>What the last purge said: its refusal's reason, or how many went; empty before any.</summary>
    public string NoteText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Reads the list.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _api.ListResources(url, owner: null, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);   // stub: the answer is not shown yet
    }
}
