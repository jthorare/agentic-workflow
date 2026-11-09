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

    /// <summary>What the last purge said: its refusal's reason, or how many went; empty before any.</summary>
    public string NoteText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Reads the list: one row per resource.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the list is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        var read = await _api.ListResources(url, owner: null, cancellationToken).ConfigureAwait(true);
        if (read.IsError)
        {
            return;
        }

        Rows.Clear();
        foreach (var resource in read.Value.Resources)
        {
            Rows.Add(new ResourceRowViewModel(resource, Purge));
        }

        IsEmpty = Rows.Count == 0;
    }

    // Stub: a purge sends nothing yet and only reads the list again; the refusal is never shown.
    private Task Purge(string owner, CancellationToken cancellationToken) =>
        _url is { } url && owner.Length > 0 ? Refresh(url, cancellationToken) : Task.CompletedTask;
}
