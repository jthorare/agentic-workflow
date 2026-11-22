using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// The concept band (<see cref="ContextPageViewModel"/>, continued): the map opens on the product's concepts, a concept opens today's bands scoped to it,
/// and the two levels below work as before. A map with no concepts, or a server without the route, opens the bands unscoped as it always did.
/// </summary>
internal sealed partial class ContextPageViewModel
{
    private bool _conceptsRead;
    private int _openGeneration;
    private string? _conceptsVersion;

    /// <summary>The first layer of the map: the product's concepts.</summary>
    public ConceptLayerViewModel Concepts { get; }

    /// <summary>Whether the concept layer is on the stage: it has concepts (or is reading them), none is open, and no query's neighbourhood, No map state or build covers it.</summary>
    public bool ShowConcepts => ShowMap && Concepts.IsOnStage && OpenedConceptId is null && !HasStack && !NoMap && !IsBuilding;

    /// <summary>The id of the concept opened; null on the concept layer and when the map has no concepts.</summary>
    internal string? OpenedConceptId { get; private set; }

    /// <summary>The back button's words.</summary>
    public string BackText { get; } = Labels.ContextConceptsBack;

    // Registered once: the whole map's crumbs and back button ask the page to go up; the layer's pin side opens the sheet and answers its footer buttons.
    private void InitConcepts()
    {
        Concepts.WirePins(Ports.OpenPin, () => _repo ?? string.Empty);
        Concepts.TryAgain = () => _ = TryRefreshAgain();
        Follow(RefreshPins);
        Whole.ConceptsRequested += (_, _) => CloseConcept();
        Whole.UpRequested += (_, _) => _ = Up();
    }

    // The page's first read: the concepts; with some, the layer is the page, else the whole map is read as before.
    // The read started last wins (#3697): each await's continuation drops its result when another first read or a repo switch began meanwhile.
    private async Task OpenFirst(CancellationToken cancellationToken)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var generation = ++_openGeneration;
        if (Whole.IsLoading)
        {
            Whole.Quiet();   // an older whole-map read still in flight is dropped when it ends, so it must not leave "Reading the map" behind
        }

        Concepts.Loading();
        ChangedConcepts();
        var read = await ReadConcepts(api, cancellationToken).ConfigureAwait(true);
        if (generation == _openGeneration)
        {
            await DrawFirst(api, read, generation, cancellationToken).ConfigureAwait(true);
        }
    }

    // The concepts' answer onto the page, then the glossary and pins, or the whole map when there are no concepts.
    private async Task DrawFirst(Uri api, Result<ContextConceptsView> read, int generation, CancellationToken cancellationToken)
    {
        _conceptsRead = ConceptsReached(read);
        var fresh = await ReadNewSuggested(read, cancellationToken).ConfigureAwait(true);
        if (generation != _openGeneration)
        {
            return;
        }

        NoteConcepts(read, fresh);
        CountNoConcept(read.IsSuccess ? read.Value : null);
        if (!Concepts.HasConcepts)
        {
            ChangedConcepts();
            await ReadWhole(cancellationToken).ConfigureAwait(true);
            return;
        }

        Head = Labels.ContextConceptsHead;
        ChangedConcepts();
        ChangedWhole();
        _conceptsVersion = read.Value.MapVersion;
        var glossary = await AskGlossary(api, read.Value.MapVersion, cancellationToken).ConfigureAwait(true);
        if (generation == _openGeneration)
        {
            ShowGlossary(glossary);
            await ReadPins(api, cancellationToken).ConfigureAwait(true);
        }
    }

    // Whether the server answered the concepts read at all (an unreachable one is read again later).
    private static bool ConceptsReached(Result<ContextConceptsView> read) => read.IsSuccess || !string.Equals(read.Error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal);

    private async Task<IReadOnlyDictionary<string, int>?> ReadNewSuggested(Result<ContextConceptsView> read, CancellationToken cancellationToken) =>
        read.IsSuccess && string.Equals(read.Value.State, "ok", StringComparison.Ordinal) ? await ReadSuggestedSeen(read.Value, cancellationToken).ConfigureAwait(true) : null;

    // Every page of the concepts, joined: the first page's state and Other, the concepts and links of all. A later page that fails keeps what was read.
    private async Task<Result<ContextConceptsView>> ReadConcepts(Uri api, CancellationToken cancellationToken)
    {
        var repo = _repo ?? string.Empty;
        var first = await Ports.Api.ContextConcepts(api, new ConceptsQuery(repo), cancellationToken).ConfigureAwait(true);
        if (first.IsError)
        {
            return first;
        }

        var pages = new List<ContextConceptsView> { first.Value };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var next = first.Value.Next; next is { Length: > 0 } && seen.Add(next);)
        {
            var page = await Ports.Api.ContextConcepts(api, new ConceptsQuery(repo, next), cancellationToken).ConfigureAwait(true);
            if (page.IsError)
            {
                break;
            }

            pages.Add(page.Value);
            next = page.Value.Next;
        }

        return Joined(pages);
    }

    // The first page's state and Other, with the concepts and links of every page.
    private static ContextConceptsView Joined(List<ContextConceptsView> pages) => pages[0] with
    {
        Concepts = [.. pages.SelectMany(p => p.Concepts ?? [])],
        Links = [.. pages.SelectMany(p => p.Links ?? [])],
        Next = null,
    };

    // The concept cards' terms. No map (the 030 refusal, or a 200 "unavailable") and a server without the route leave the cards as the concepts read left them;
    // any other refusal is said in one line with its code and message.
    private async Task ReadGlossary(Uri api, string? mapVersion, CancellationToken cancellationToken) =>
        ShowGlossary(await AskGlossary(api, mapVersion, cancellationToken).ConfigureAwait(true));

    private Task<Result<ConceptGlossaryView>> AskGlossary(Uri api, string? mapVersion, CancellationToken cancellationToken) =>
        Ports.Api.ContextConceptGlossary(api, new ConceptGlossaryQuery(_repo ?? string.Empty, mapVersion), cancellationToken);

    private void ShowGlossary(Result<ConceptGlossaryView> read)
    {
        if (read.IsSuccess)
        {
            if (!string.Equals(read.Value.State, "unavailable", StringComparison.Ordinal))
            {
                Concepts.ShowTerms(read.Value);
            }
        }
        else if (!string.Equals(read.Error.Code, NoMapCode, StringComparison.Ordinal) && !IsNotServed(read.Error))
        {
            Concepts.TermsUnavailable(read.Error.Code, read.Error.Message);
        }

        ChangedConcepts();
    }

    /// <summary>Reads the page's top again (Retry, and when a build has ended): the concepts when none is open, else the open concept's map.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal Task ReadMapTop(CancellationToken cancellationToken) => OpenedConceptId is null ? OpenFirst(cancellationToken) : ReadWhole(cancellationToken);

    private void NoteConcepts(Result<ContextConceptsView> read, IReadOnlyDictionary<string, int>? newSuggested = null)
    {
        if (read.IsError)
        {
            if (IsNotServed(read.Error) || string.Equals(read.Error.Code, NoMapCode, StringComparison.Ordinal))
            {
                Concepts.NotServed();   // no map: the No map state says it, a line over it would only repeat it
            }
            else
            {
                Concepts.ReadFailed(read.Error.Code);
            }

            return;
        }

        switch (read.Value.State)
        {
            case "ok":
                Concepts.Show(read.Value, newSuggested);
                break;
            case "building":
                Concepts.Building();
                break;
            case "unavailable" when string.Equals(read.Value.Code, NoMapCode, StringComparison.Ordinal):
                Concepts.NotServed();   // the No map state says it
                break;
            case "unavailable":
                Concepts.Unavailable(read.Value.Code ?? SindriUiErrors.BadAnswer.Code);
                break;
            default:
                Concepts.Empty();
                break;
        }
    }

    // A server without the route answers 404: a refusal of kind not found, or a bare 404.
    private static bool IsNotServed(Error error) =>
        error.Kind == ErrorKind.NotFound || error.IsNotFound();

    /// <summary>Opens a concept: the three bands, scoped to it.</summary>
    /// <param name="id">The concept's id ("other" for the unassigned nodes).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the scoped map is drawn.</returns>
    internal Task OpenConcept(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var name = Concepts.Find(id)?.Name ?? id;
        CloseInspector();
        ForgetRead();
        AnchorText = string.Empty;
        _ = Concepts.EndSearch();
        OpenedConceptId = id;
        Whole.EnterConcept(name, ConceptKey(id));
        ChangedConcepts();
        return ReadWhole(cancellationToken);
    }

    // The concept's node key on the map: its id, except "other", which is no concept.
    private static string? ConceptKey(string id) => string.Equals(id, "other", StringComparison.Ordinal) ? null : id;

    /// <summary>Goes back to the concepts: the scoped map goes and the layer shows again.</summary>
    internal void CloseConcept()
    {
        if (OpenedConceptId is null)
        {
            return;
        }

        CloseInspector();
        Whole.LeaveConcept();
        OpenedConceptId = null;
        AnchorText = string.Empty;
        ForgetRead();
        Head = Labels.ContextConceptsHead;
        ChangedConcepts();
        ChangedWhole();
        OnChanged(nameof(IsZoomedIn));
    }

    /// <summary>Goes up one layer: a node's panel, a query, an open feature, then the concept.</summary>
    /// <returns>True when it went up.</returns>
    internal bool Up()
    {
        if (Back() || Whole.ExitDrill())
        {
            return true;
        }

        if (Whole.IsAreaOpen)
        {
            MapHome();
            return true;
        }

        if (OpenedConceptId is null)
        {
            return false;
        }

        CloseConcept();
        return true;
    }

}
