using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>A query zooms into the whole map (<see cref="ContextPageViewModel"/>, continued; whole-map spec §3.1 "Zoomed in"): the answer's nodes stand out on the map, the camera eases to them, Esc goes back.</summary>
internal sealed partial class ContextPageViewModel
{
    private const int ReadBatch = 8;          // areas read at once
    private const int SoftNodeGuard = 5000;   // opening stops once this many nodes are drawn; the rest are counted
    private const int MaxLocate = 100;

    private IReadOnlyCollection<string> _unopenedHitAreas = [];   // the features a search touched that stay tiles

    /// <summary>Whether the camera is on a query's answer.</summary>
    public bool IsZoomedIn => Whole.IsZoomed;

    // After the existing read drew its answer: it is shown as a zoom onto the whole map when the map is there (the map behind it stays), else as the neighbourhood.
    private async Task ZoomIntoWhole(Uri api, CancellationToken cancellationToken)
    {
        if (!Whole.IsReady || Map is not { } map)
        {
            return;
        }

        var hits = await MapSearch.Search(AnchorText, cancellationToken).ConfigureAwait(true);
        IReadOnlyList<string> keys = [.. hits.Select(h => h.Key)];
        _unopenedHitAreas = [];
        var zoomed = Whole.AnswerByArea ? await ZoomIntoAreas(api, keys, cancellationToken).ConfigureAwait(true) : Whole.Zoom(keys);
        if (zoomed || hits.Count == 0 || _unopenedHitAreas.Count > 0)
        {
            Whole.ApplySearch(hits, _unopenedHitAreas, zoomed);   // an answer with none of its items on the map is not a search
        }

        if (!zoomed)
        {
            Problem = Labels.ContextWholeNoMatch;
        }

        Head = Whole.Head;
        ChangedWhole();
        OnChanged(nameof(IsZoomedIn));
    }

    // By feature: where the answer's nodes sit is read with locate=, every feature they touch is opened with area= (eight reads at once, most-touched first, until the drawn nodes pass a soft guard), and any left over are counted.
    private async Task<bool> ZoomIntoAreas(Uri api, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        if (Whole.Answer is not { } overview)
        {
            return false;
        }

        var located = await Ports.Api.WholeMap(api, new WholeMapQuery("areas", Area: null, [.. keys.Take(MaxLocate)], MapVersion: null, Draft: false, Repo: _repo, Concept: OpenedConceptId), cancellationToken).ConfigureAwait(true);
        if (located.IsError)
        {
            Problem = located.Error.Message;
            return true;   // the refusal is the banner; nothing else says "no match"
        }

        // why: the Where above drops every located entry with no area, so no group key is null
        var touched = (located.Value.Located ?? []).Where(l => l.Area is not null).GroupBy(l => l.Area, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key!).ToList();
        if (touched.Count == 0)
        {
            return false;
        }

        var reads = await ReadAreas(api, touched, cancellationToken).ConfigureAwait(true);
        if (reads.FirstOrDefault(r => r.IsError) is { } failed)
        {
            Problem = failed.Error.Message;
            return true;
        }

        _unopenedHitAreas = [.. touched.Skip(reads.Count)];
        return Whole.ZoomIntoAreas(overview, [.. reads.Select(r => r.Value)], keys, touched.Count - reads.Count);
    }

    // Eight areas at a time, most-touched first, until the drawn nodes pass the soft guard.
    private async Task<List<Result<WholeMapView>>> ReadAreas(Uri api, List<string> touched, CancellationToken cancellationToken)
    {
        var reads = new List<Result<WholeMapView>>();
        var drawn = 0;
        for (var next = 0; next < touched.Count && drawn < SoftNodeGuard; next += ReadBatch)
        {
            var batch = await Task.WhenAll(touched.Skip(next).Take(ReadBatch).Select(area => Ports.Api.WholeMap(api, new WholeMapQuery("nodes", area, [], MapVersion: null, Draft: false, Repo: _repo, Concept: OpenedConceptId), cancellationToken))).ConfigureAwait(true);
            reads.AddRange(batch);
            drawn += batch.Sum(r => r.IsError ? 0 : r.Value.Nodes?.Count ?? 0);
        }

        return reads;
    }

    /// <summary>
    /// A click on a node of the whole map: a node of the answer the camera is on opens in the inspector; any other node (dimmed behind the answer, or any node when nothing is zoomed to)
    /// becomes the new starting point, read like a picked suggestion, and the camera eases to its answer.
    /// </summary>
    /// <param name="key">The node's key.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>When the inspector is filled or the new answer is drawn.</returns>
    internal Task ChooseWholeNode(string key, CancellationToken cancellationToken = default) =>
        Whole.IsZoomed && Whole.HitKeys.Contains(key) && Map is { } map && map.Nodes.Any(n => string.Equals(n.Key, key, StringComparison.Ordinal))
            ? Select(key, cancellationToken)
            : Pick(key, cancellationToken);

    /// <summary>Esc: the inspector closes first, then the camera goes back to the whole map.</summary>
    /// <returns>True when it did something.</returns>
    internal bool Back()
    {
        if (Whole.Panel.Close())
        {
            return true;   // Esc closes the node panel first and the pin stays
        }

        if (Whole.ClearPin())
        {
            return true;   // then it lets a pinned highlight go first
        }

        if (InspectorOpen)
        {
            CloseInspector();
            return true;
        }

        if (Whole.SearchActive || Concepts.SearchActive)
        {
            ClearSearch();   // the box empties too
            return true;
        }

        return BackToWholeMap();
    }

    /// <summary>The clear button and Esc: the box empties, nothing dims and the camera fits the whole map.</summary>
    internal void ClearSearch()
    {
        AnchorText = string.Empty;   // an empty box ends the search itself
        _ = BackToWholeMap();
    }

    /// <summary>Back to the whole map: the answer stops standing out and the camera fits the world again.</summary>
    /// <returns>True when the camera was on an answer.</returns>
    internal bool BackToWholeMap()
    {
        var ended = Concepts.EndSearch();   // the concept layer's own search ends the same way
        if (ended)
        {
            ChangedConcepts();
        }

        if (!Whole.IsZoomed && !Whole.SearchActive)
        {
            return ended;
        }

        CloseInspector();
        Whole.Unzoom();
        ForgetRead();
        Head = Whole.Head;
        ChangedWhole();
        OnChanged(nameof(IsZoomedIn));
        return true;
    }

    // The answer's neighbourhood and the words that came with it go; the whole map is what is left.
    private void ForgetRead()
    {
        ForgetSelection();
        Map = null;
        Missing = [];
        Choices = [];
        Problem = string.Empty;
        Refresh();
    }
}
