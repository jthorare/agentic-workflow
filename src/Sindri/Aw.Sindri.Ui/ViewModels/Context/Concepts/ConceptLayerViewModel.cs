using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>
/// The concept band: the first layer of the context map. It holds the product's concepts (name, summary, size) and a synthetic Other for the unassigned nodes.
/// A search filters it the way the map's own search does: the matches and the concepts they link to stay, the rest is gone. It is loading while the read is in flight,
/// and when the map has no concepts it says why in one line and the page opens the bands unscoped.
/// </summary>
internal sealed class ConceptLayerViewModel : ObservableObject
{
    private readonly Action<string> _open;
    private IReadOnlyList<ConceptTileViewModel> _all = [];
    private IReadOnlyList<ConceptLink> _links = [];
    private HashSet<string>? _kept;

    internal ConceptLayerViewModel(Action<string> open)
    {
        _open = open;
    }

    /// <summary>The concepts drawn, the concept of the unassigned nodes last; while a search is on, only the ones it kept.</summary>
    public IReadOnlyList<ConceptTileViewModel> Tiles => _kept is null ? _all : [.. _all.Where(t => _kept.Contains(t.Id))];

    /// <summary>The line over the cards when the glossary has no terms yet; empty otherwise.</summary>
    public string TermsNote { get; private set; } = string.Empty;

    /// <summary>Whether the terms note has words.</summary>
    public bool HasTermsNote => TermsNote.Length > 0;

    /// <summary>Whether the concepts are being read.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>The line over the stage while reading.</summary>
    public string LoadingText { get; } = Labels.ContextWholeLoading;

    /// <summary>The list's accessible name.</summary>
    public string ListName { get; } = Labels.ContextConceptsListName;

    /// <summary>Whether the map has concepts to show.</summary>
    internal bool HasConcepts => _all.Count > 0;

    /// <summary>Whether the layer takes the stage: it is reading, or has concepts.</summary>
    internal bool IsOnStage => IsLoading || HasConcepts;

    /// <summary>The line that says why the map is shown without concepts; empty when there is none to say.</summary>
    public string Note { get; private set; } = string.Empty;

    /// <summary>Whether the note has words.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Whether a search filters the concepts.</summary>
    internal bool SearchActive { get; private set; }

    /// <summary>Whether the search kept no concept.</summary>
    internal bool NoMatches { get; private set; }

    /// <summary>The concept with an id, or null.</summary>
    /// <param name="id">The concept's id.</param>
    /// <returns>Its tile.</returns>
    internal ConceptTileViewModel? Find(string id) => _all.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    /// <summary>A read starts.</summary>
    internal void Loading()
    {
        (IsLoading, Note) = (true, string.Empty);
        Changed();
    }

    /// <summary>The concepts were read.</summary>
    /// <param name="view">The ok answer.</param>
    internal void Show(ContextConceptsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var tiles = (view.Concepts ?? []).Select(c => new ConceptTileViewModel(c.Id, c.Name, c.Summary, c.Size, string.Equals(c.Source, "override", StringComparison.Ordinal), terms: null, _open)).ToList();
        if (view.Other is { } other && other.Size.Total > 0)
        {
            tiles.Add(new ConceptTileViewModel(other.Id, other.Name.Length > 0 ? other.Name : Labels.ContextWholeBandOther, string.Empty, other.Size, setByPerson: false, terms: null, _open));
        }

        if (tiles.Count == 0)
        {
            Say(Labels.ContextConceptsEmpty);
            return;
        }

        (_all, _links, IsLoading, Note) = (tiles, view.Links ?? [], false, string.Empty);
        ResetSearch();
        Changed();
    }

    /// <summary>The glossary was read: each card takes its concept's terms (matched by id); an empty glossary says so in one line.</summary>
    /// <param name="view">The glossary.</param>
    internal void ShowTerms(ConceptGlossaryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var termsById = view.Concepts.ToDictionary(c => c.Id, c => c.Terms, StringComparer.Ordinal);
        _all = [.. _all.Select(t => termsById.TryGetValue(t.Id, out var terms) ? t.WithTerms(terms) : t)];
        TermsNote = string.Equals(view.State, "ok", StringComparison.Ordinal) ? string.Empty : Labels.ContextConceptsNoTerms;
        Changed();
    }

    /// <summary>The glossary was refused: one line with the code and the message; the cards keep what they have.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The refusal's message.</param>
    internal void TermsUnavailable(string code, string message)
    {
        TermsNote = Labels.ContextConceptsTermsUnavailable(code, message);
        Changed();
    }

    /// <summary>The server has no such route (a 404): the same as no concepts, with nothing to say.</summary>
    internal void NotServed() => Say(string.Empty);

    /// <summary>The map has no concepts yet.</summary>
    internal void Empty() => Say(Labels.ContextConceptsEmpty);

    /// <summary>The concepts are still being built.</summary>
    internal void Building() => Say(Labels.ContextConceptsBuilding);

    /// <summary>The concepts cannot be read.</summary>
    /// <param name="code">The error code.</param>
    internal void Unavailable(string code) => Say(Labels.ContextConceptsUnavailable(code));

    /// <summary>Forgets everything (another repo is chosen).</summary>
    internal void Reset() => Say(string.Empty);

    /// <summary>The concepts whose name or summary holds the words.</summary>
    /// <param name="text">What was typed.</param>
    /// <returns>The ids of the concepts that match.</returns>
    internal IReadOnlySet<string> Matching(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : _all.Where(t => t.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || t.Summary.Contains(text, StringComparison.OrdinalIgnoreCase) || t.Terms.Any(term => term.Contains(text, StringComparison.OrdinalIgnoreCase))).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>A search resolved: the concepts that match stay, with the ones they link to; the rest is gone.</summary>
    /// <param name="matches">The ids of the concepts that match.</param>
    internal void ApplySearch(IReadOnlySet<string> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);
        var kept = new HashSet<string>(matches, StringComparer.Ordinal);
        foreach (var link in _links)
        {
            if (matches.Contains(link.From))
            {
                _ = kept.Add(link.To);
            }

            if (matches.Contains(link.To))
            {
                _ = kept.Add(link.From);
            }
        }

        (_kept, SearchActive, NoMatches) = (kept, true, matches.Count == 0);
        Changed();
    }

    /// <summary>The search ends: every concept is drawn again.</summary>
    /// <returns>True when a search was on.</returns>
    internal bool EndSearch()
    {
        if (!SearchActive)
        {
            return false;
        }

        ResetSearch();
        Changed();
        return true;
    }

    private void Say(string note)
    {
        TermsNote = string.Empty;
        _all = [];
        _links = [];
        (IsLoading, Note) = (false, note);
        ResetSearch();
        Changed();
    }

    private void ResetSearch() => (_kept, SearchActive, NoMatches) = (null, false, false);

    private void Changed()
    {
        foreach (var name in (string[])[nameof(Tiles), nameof(IsLoading), nameof(Note), nameof(HasNote), nameof(SearchActive), nameof(NoMatches), nameof(TermsNote), nameof(HasTermsNote)])
        {
            OnChanged(name);
        }
    }
}
