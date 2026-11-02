using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The feature picker (<see cref="PinSheetViewModel"/>, continued, §3.6): a search over the map's features, read after the typing stops.</summary>
internal sealed partial class PinSheetViewModel
{
    /// <summary>How long the typing rests before the search reads.</summary>
    internal static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(250);

    private const int SearchLimit = 20;
    private int _searchGeneration;

    /// <summary>The picker's search text.</summary>
    public string FeatureSearch
    {
        get;
        set
        {
            if (Change(ref field, value))
            {
                Searching = SearchAfterDebounce(value, CancellationToken.None);
            }
        }
    } = string.Empty;

    /// <summary>The latest search: it ends once its answer is drawn (or a newer search took its place).</summary>
    internal Task Searching { get; private set; } = Task.CompletedTask;

    /// <summary>The features the search found that the pin doesn't hold yet.</summary>
    public System.Collections.ObjectModel.ObservableCollection<PinFeatureRow> FeatureMatches { get; } = [];

    /// <summary>The picker's line: "Loading features…", the failure, or what an empty answer means; empty while it has matches.</summary>
    public string FeaturesNote { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the picker has a line.</summary>
    public bool HasFeaturesNote => FeaturesNote.Length > 0;

    /// <summary>True when the picker's read failed: Try again shows.</summary>
    public bool CanTryAgain { get; private set => Set(ref field, value); }

    /// <summary>Reads the features again after a failure.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal Task TryAgain(CancellationToken cancellationToken) => ReadFeatures(FeatureSearch.Trim(), cancellationToken);

    /// <summary>Adds a found feature to the pin, ticked.</summary>
    /// <param name="match">The row of <see cref="FeatureMatches"/>.</param>
    internal void AddFeature(PinFeatureRow match)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (Features.All(f => !string.Equals(f.Key, match.Key, StringComparison.Ordinal)))
        {
            Features.Add(Watched(new PinFeatureRow(match.Key, match.Label, ticked: true)));
        }

        _ = FeatureMatches.Remove(match);
        Edited();
    }

    // A search reads once the typing rests; a newer search drops this one.
    private async Task SearchAfterDebounce(string text, CancellationToken cancellationToken)
    {
        if (_ports is null)
        {
            return;
        }

        var generation = ++_searchGeneration;
        try
        {
            await Task.Delay(SearchDebounce, _ports.Clock, cancellationToken).ConfigureAwait(true);
            if (generation == _searchGeneration)
            {
                await ReadFeatures(text.Trim(), cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // the sheet closed while it waited
        }
    }

    private async Task ReadFeatures(string search, CancellationToken cancellationToken)
    {
        if (!HasPicker || Ports.ApiUrl() is not { } url)
        {
            return;
        }

        var generation = ++_searchGeneration;
        (FeaturesNote, CanTryAgain) = (Labels.PinSheetFeaturesLoading, false);
        OnChanged(nameof(HasFeaturesNote));
        var read = await Ports.Api.ContextPinSuggest(url, new PinSuggestQuery(_repo, Search: search, Limit: SearchLimit), cancellationToken).ConfigureAwait(true);
        if (generation != _searchGeneration)
        {
            return;
        }

        FeatureMatches.Clear();
        if (!read.IsSuccess || read.Value.Features is not { } found)
        {
            (FeaturesNote, CanTryAgain) = (Labels.PinSheetFeaturesFailed, true);
        }
        else
        {
            foreach (var feature in found.Where(f => Features.All(h => !string.Equals(h.Key, f.Key, StringComparison.Ordinal))))
            {
                FeatureMatches.Add(new PinFeatureRow(feature.Key, feature.Label, ticked: false));
            }

            FeaturesNote = found.Count > 0 ? string.Empty : search.Length == 0 ? Labels.PinSheetNoFeatures : Labels.PinSheetNoFeatureMatches(search);
        }

        OnChanged(nameof(HasFeaturesNote));
    }
}
