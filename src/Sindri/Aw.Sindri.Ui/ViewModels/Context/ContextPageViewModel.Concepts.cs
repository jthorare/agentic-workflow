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

    /// <summary>The first layer of the map: the product's concepts.</summary>
    public ConceptLayerViewModel Concepts { get; }

    /// <summary>Whether the concept layer is on the stage: it has concepts (or is reading them), none is open, and no query's neighbourhood, No map state or build covers it.</summary>
    public bool ShowConcepts => ShowMap && Concepts.IsOnStage && OpenedConceptId is null && !HasStack && !NoMap && !IsBuilding;

    /// <summary>The id of the concept opened; null on the concept layer and when the map has no concepts.</summary>
    internal string? OpenedConceptId { get; private set; }

    /// <summary>The back button's words.</summary>
    public string BackText { get; } = Labels.ContextConceptsBack;

    // Registered once: the whole map's crumbs and back button ask the page to go up.
    private void InitConcepts()
    {
        Whole.ConceptsRequested += (_, _) => CloseConcept();
        Whole.UpRequested += (_, _) => _ = Up();
    }

    // The page's first read: the concepts; with some, the layer is the page, else the whole map is read as before.
    private async Task OpenFirst(CancellationToken cancellationToken)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        Concepts.Loading();
        ChangedConcepts();
        var read = await ReadConcepts(api, cancellationToken).ConfigureAwait(true);
        _conceptsRead = read.IsSuccess || !string.Equals(read.Error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal);
        NoteConcepts(read);
        if (Concepts.HasConcepts)
        {
            Head = Labels.ContextConceptsHead;
            ChangedConcepts();
            ChangedWhole();
            await ReadGlossary(api, read.Value.MapVersion, cancellationToken).ConfigureAwait(true);
            return;
        }

        ChangedConcepts();
        await ReadWhole(cancellationToken).ConfigureAwait(true);
    }

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
    private async Task ReadGlossary(Uri api, string? mapVersion, CancellationToken cancellationToken)
    {
        var read = await Ports.Api.ContextConceptGlossary(api, new ConceptGlossaryQuery(_repo ?? string.Empty, mapVersion), cancellationToken).ConfigureAwait(true);
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

    private void NoteConcepts(Result<ContextConceptsView> read)
    {
        if (read.IsError)
        {
            if (IsNotServed(read.Error))
            {
                Concepts.NotServed();
            }
            else
            {
                Concepts.Unavailable(read.Error.Code);
            }

            return;
        }

        switch (read.Value.State)
        {
            case "ok":
                Concepts.Show(read.Value);
                break;
            case "building":
                Concepts.Building();
                break;
            case "unavailable":
                Concepts.Unavailable(read.Value.Code ?? SindriUiErrors.BadAnswer.Code);
                break;
            default:
                Concepts.Empty();
                break;
        }
    }

    // A server without the route answers 404: a refusal of kind not found, or the client's own "answered 404".
    private static bool IsNotServed(Error error) =>
        error.Kind == ErrorKind.NotFound || (string.Equals(error.Code, SindriUiErrors.BadStatus.Code, StringComparison.Ordinal) && error.Message.Contains("404", StringComparison.Ordinal));

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
        Whole.EnterConcept(name);
        ChangedConcepts();
        return ReadWhole(cancellationToken);
    }

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
        if (Back())
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

    // A query at the concept layer: the concepts whose name or summary hold the words, and the ones that hold a node the words (or the anchors they resolve to) name.
    private async Task SearchConcepts(Uri api, List<string> words, int generation, CancellationToken cancellationToken)
    {
        var matches = new HashSet<string>(Concepts.Matching(AnchorText.Trim()), StringComparer.Ordinal);
        var resolved = await Resolve(api, words, generation, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }

        if (resolved is { } found)
        {
            var located = await Ports.Api.WholeMap(api, new WholeMapQuery("areas", Area: null, [.. found.Anchors.Take(MaxLocate)], MapVersion: null, Draft: false, Repo: _repo), cancellationToken).ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }

            if (located.IsSuccess)
            {
                matches.UnionWith((located.Value.Located ?? []).SelectMany(l => l.Concept ?? []));
            }
        }

        Concepts.ApplySearch(matches);
        ChangedConcepts();
    }

    private void ChangedConcepts()
    {
        OnChanged(nameof(ShowConcepts));
        OnChanged(nameof(ShowWhole));
        OnChanged(nameof(ShowWholeStatus));
        OnChanged(nameof(OpenedConceptId));
        OnChanged(nameof(Head));
        OnChanged(nameof(HasHead));
        OnChanged(nameof(SearchNote));
        OnChanged(nameof(HasSearchNote));
    }
}
