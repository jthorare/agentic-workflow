namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The inspector's "Where it comes from" (F3 spec 1 §4): the node's source joined with GET /sindri/context/sources, and how sure the map
/// is. A per-node citation list is not served, so a node has one row.
/// </summary>
internal sealed class SourcesPanelViewModel(ContextPorts ports) : ObservableObject
{
    private int _generation;

    /// <summary>What the stage reads through.</summary>
    internal ContextPorts Ports { get; } = ports;

    /// <summary>The section's title.</summary>
    public string Title { get; } = Labels.ContextInspSource;

    /// <summary>The columns' words.</summary>
    public string SourceColumn { get; } = Labels.ContextInspSrcSource;

    /// <summary>The when column.</summary>
    public string WhenColumn { get; } = Labels.ContextInspSrcWhen;

    /// <summary>The how sure column.</summary>
    public string SureColumn { get; } = Labels.ContextInspSrcSure;

    /// <summary>The rows: one for the chosen node.</summary>
    public IReadOnlyList<SourceRowViewModel> Rows { get; private set; } = [];

    /// <summary>Whether there is a row to show.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>Shows a node's source.</summary>
    /// <param name="map">The map the node is on.</param>
    /// <param name="key">The node's key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the row is shown.</returns>
    internal async Task Show(ContextMapViewModel map, string key, CancellationToken cancellationToken = default)
    {
        var generation = ++_generation;
        if (map.Node(key)?.Layout is not { } node)
        {
            return;
        }

        var sure = Labels.ContextInspSure(node.Confirmed, node.Provenance, node.Confidence);
        var origin = new SourceRowViewModel(Labels.ContextInspOrigin(node.Provenance), string.Empty, sure);
        Put(origin);
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var read = await Ports.Api.Sources(api, cancellationToken).ConfigureAwait(true);
        if (generation != _generation || read.IsError)
        {
            return;   // not served: the row stays the one that says who found the node
        }

        if (read.Value.Sources.FirstOrDefault(s => node.Detail.Length > 0 && string.Equals(s.Source, node.Detail, StringComparison.Ordinal)) is { } source)
        {
            var when = source.LastSync is { } at ? Labels.ContextInspSynced(Ports.Clock.GetUtcNow() - at) : Labels.ContextInspNotSynced;
            Put(new SourceRowViewModel(source.Source, when, sure));
        }
    }

    /// <summary>Clears the panel.</summary>
    internal void Close()
    {
        _generation++;
        Rows = [];
        OnChanged(nameof(Rows));
        OnChanged(nameof(HasRows));
    }

    private void Put(SourceRowViewModel row)
    {
        Rows = [row];
        OnChanged(nameof(Rows));
        OnChanged(nameof(HasRows));
    }
}
