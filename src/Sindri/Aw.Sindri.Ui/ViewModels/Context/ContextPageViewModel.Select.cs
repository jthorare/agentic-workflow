namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Choosing a node and the inspector it opens (F3 spec 1 §8.3): click, J and K, Esc.</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>Whether the inspector is on the stage: a node is chosen.</summary>
    public bool InspectorOpen => Map?.SelectedKey is not null;

    /// <summary>Chooses a node: it is marked on the map and the inspector reads it.</summary>
    /// <param name="key">The node's key; a key the map does not hold changes nothing.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>When the inspector is filled.</returns>
    internal Task Select(string key, CancellationToken cancellationToken = default) =>
        Map is { } map && map.Select(key) ? Inspect(map, key, cancellationToken) : Task.CompletedTask;

    /// <summary>J: the next node in reading order.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>When the inspector is filled.</returns>
    internal Task SelectNext(CancellationToken cancellationToken = default) =>
        Map?.NextKey() is { } key ? Select(key, cancellationToken) : Task.CompletedTask;

    /// <summary>K: the previous node in reading order.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>When the inspector is filled.</returns>
    internal Task SelectPrevious(CancellationToken cancellationToken = default) =>
        Map?.PreviousKey() is { } key ? Select(key, cancellationToken) : Task.CompletedTask;

    /// <summary>Esc, or a click on the empty canvas: the inspector closes and nothing is chosen.</summary>
    internal void CloseInspector()
    {
        _ = Map?.Select(key: null);
        Node.Close();
        Sources.Close();
        OnChanged(nameof(InspectorOpen));
    }

    // A new read starts with nothing chosen: the old node's card must not outlive its map.
    private void ForgetSelection()
    {
        Node.Close();
        Sources.Close();
        OnChanged(nameof(InspectorOpen));
    }

    private async Task Inspect(ContextMapViewModel map, string key, CancellationToken cancellationToken)
    {
        OnChanged(nameof(InspectorOpen));
        await Task.WhenAll(Node.Show(map, key, cancellationToken), Sources.Show(map, key, cancellationToken)).ConfigureAwait(true);
    }
}
