using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Memory;

namespace Aw.Sindri.Ui.ViewModels.Memory;

/// <summary>
/// The search box of one store and what it found: the fact rows, the mode chip (semantic, or keyword with the API's note saying why) and the count.
/// The facts of a store are only ever seen through a search, as in memory_search.
/// </summary>
internal sealed class MemorySearchViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly Func<Uri?> _url;
    private readonly string _store;
    private readonly TimeProvider _clock;
    private readonly Func<string, CancellationToken, Task> _forgetFact;
    private readonly Func<string, CancellationToken, Task> _forgetTag;
    private readonly BusyFlag _forgetBusy;
    private int _searches;

    /// <summary>The search over its ports.</summary>
    /// <param name="api">The API.</param>
    /// <param name="url">The API's base URL, or null while it isn't known.</param>
    /// <param name="store">The store searched.</param>
    /// <param name="clock">The clock expiry is read against.</param>
    /// <param name="forgetFact">Sends the forget for one fact id.</param>
    /// <param name="forgetTag">Sends the forget for one tag.</param>
    /// <param name="forgetBusy">The store's forget flag, shared with its rows; a new one when none is given.</param>
    internal MemorySearchViewModel(ISindriApi api, Func<Uri?> url, string store, TimeProvider clock, Func<string, CancellationToken, Task> forgetFact, Func<string, CancellationToken, Task> forgetTag, BusyFlag? forgetBusy = null)
    {
        (_api, _url, _store, _clock, _forgetFact, _forgetTag) = (api, url, store, clock, forgetFact, forgetTag);
        _forgetBusy = forgetBusy ?? new BusyFlag();
    }

    /// <summary>The search box's text.</summary>
    public string Query { get; set => Set(ref field, value ?? string.Empty); } = string.Empty;

    /// <summary>The match level chosen.</summary>
    public string SelectedLevel { get; set => Set(ref field, value); } = Labels.MemoryDefaultLevel;

    /// <summary>The most facts a search returns (memory_search's limit); the API's default is not sent.</summary>
    public int SelectedLimit { get; set => Set(ref field, value); } = Labels.MemoryDefaultLimit;

    /// <summary>The levels to choose from.</summary>
    /// <summary>The search box's accessible name.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public IReadOnlyList<string> LevelChoices => Labels.MemoryLevels;

    /// <summary>The limits to choose from.</summary>
    public IReadOnlyList<int> LimitChoices => Labels.MemoryLimits;

    public string FieldName => Labels.MemorySearchField;

    /// <summary>The search action's words.</summary>
    public string ActionText => Labels.MemorySearchAction;

    /// <summary>The level choice's accessible name.</summary>
    public string LevelName => Labels.MemoryLevelField;

    /// <summary>The limit choice's accessible name.</summary>
    public string LimitName => Labels.MemoryLimitField;
#pragma warning restore CA1822, MA0041

    /// <summary>The facts the last search found.</summary>
    public ObservableCollection<MemoryFactRowViewModel> Rows { get; } = [];

    /// <summary>True once a search has answered.</summary>
    public bool HasSearched { get; private set => Set(ref field, value); }

    /// <summary>"Semantic match" or "Keyword match".</summary>
    public string ModeText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The API's note on the answer (why it fell back to keywords); empty when there is none.</summary>
    public string NoteText { get; private set { Set(ref field, value); OnChanged(nameof(HasNote)); } } = string.Empty;

    /// <summary>True when the answer has a note.</summary>
    public bool HasNote => NoteText.Length > 0;

    /// <summary>"2 facts found" or "Nothing matched."</summary>
    public string ResultText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The sentence for a failed search; empty otherwise.</summary>
    public string Error { get; private set { Set(ref field, value); OnChanged(nameof(HasError)); } } = string.Empty;

    /// <summary>True when the last search failed.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Searches the store for <see cref="Query"/>. An empty query searches nothing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Run(CancellationToken cancellationToken)
    {
        var query = Query.Trim();
        if (query.Length == 0)
        {
            return;
        }

        if (_url() is not { } url)
        {
            Error = Labels.MemorySearchFailed(SindriUiErrors.Unreachable.With("Sindri isn't reachable yet"));
            return;
        }

        var search = ++_searches;
        var answer = await _api.SearchMemory(url, new MemorySearch(_store, query, SelectedLevel, Limit()), cancellationToken).ConfigureAwait(true);
        if (search != _searches)
        {
            return;   // a newer search is the one to show
        }

        if (!answer.IsSuccess)
        {
            Error = Labels.MemorySearchFailed(answer.Error);   // the facts shown stay
            return;
        }

        var view = answer.Value;
        Error = string.Empty;
        HasSearched = true;
        ModeText = string.Equals(view.Mode, "semantic", StringComparison.Ordinal) ? Labels.MemoryModeSemantic
            : string.Equals(view.Mode, "keyword", StringComparison.Ordinal) ? Labels.MemoryModeKeyword
            : view.Mode;
        NoteText = view.Note ?? string.Empty;
        Rows.Clear();
        foreach (var fact in view.Facts)
        {
            Rows.Add(new MemoryFactRowViewModel(fact, _clock, _forgetFact, _forgetTag, _forgetBusy));
        }

        ResultText = Rows.Count == 0 ? Labels.MemoryNothingFound : Labels.MemoryFound(Rows.Count);
    }

    // The limit sent: none for the API's own default, so a search at the default asks what it always asked.
    private int? Limit() => SelectedLimit == Labels.MemoryDefaultLimit ? null : SelectedLimit;

    /// <summary>Takes the rows the API confirmed forgotten out of the list.</summary>
    /// <param name="forgotten">Which rows are gone.</param>
    internal void Remove(Func<MemoryFactRowViewModel, bool> forgotten)
    {
        _searches++;   // a search still on its way was asked before this forget; its answer may hold the forgotten fact
        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (forgotten(Rows[i]))
            {
                Rows.RemoveAt(i);
            }
        }

        ResultText = Rows.Count == 0 ? Labels.MemoryNothingFound : Labels.MemoryFound(Rows.Count);
    }

    /// <summary>When the facts' expiry is read against now: the facts expired at this moment.</summary>
    /// <param name="row">The row.</param>
    /// <returns>True when the fact has expired.</returns>
    internal bool IsExpired(MemoryFactRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Fact.ExpiresAt is { } at && at <= _clock.GetUtcNow();
    }
}
