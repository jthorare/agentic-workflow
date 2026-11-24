using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The page's first state (<see cref="ContextPageViewModel"/>, continued; whole-map spec §3.1): the whole map is read when the page is first shown, with no typing.</summary>
internal sealed partial class ContextPageViewModel
{
    private bool _wholeWanted;
    private int _rereadNumber;

    /// <summary>The whole map the page opens on.</summary>
    public WholeMapViewModel Whole { get; } = new();

    /// <summary>Whether the whole map is on the stage: it is read, and neither a query's neighbourhood, the No map state nor a running build covers it.</summary>
    public bool ShowWhole => ShowMap && Whole.IsReady && !NoMap && !IsBuilding;

    /// <summary>Whether the quiet loading row or the failure row shows over the stage.</summary>
    public bool ShowWholeStatus => ShowMap && !HasStack && !NoMap && !IsBuilding && (Whole.IsLoading || Whole.IsFailed);

    /// <summary>The note by the search box when nothing on the map matches.</summary>
    public string SearchNote => Whole.NoMatches || Concepts.NoMatches ? Labels.ContextSearchNoMatches : string.Empty;

    /// <summary>Whether the note shows.</summary>
    public bool HasSearchNote => Whole.NoMatches || Concepts.NoMatches;

    /// <summary>The clear button's words.</summary>
    public string SearchClearText { get; } = Labels.ContextSearchClear;

    // Registered once: whenever the control plane is reachable and the page was shown, a map that is not read yet is read (the first open may have come before the connection).
    private void InitWhole()
    {
        Whole.AreaOpenRequested += (_, e) => _ = OpenWholeArea(e.Value, CancellationToken.None);
        Whole.AreaPageRequested += (_, e) => _ = OpenWholeAreaPage(Whole.AreaKey, e.Value, CancellationToken.None);
        Whole.ConceptListRequested += (_, e) => _ = OpenConceptList(e.Value.Tab, e.Value.Page, CancellationToken.None);
        Whole.HomeRequested += (_, _) => MapHome();
        Whole.Panel.Launcher = Ports.Launcher ?? NullLauncher.Instance;
        Whole.Panel.Clipboard = Ports.Clipboard;
        Whole.Panel.Loader = async (key, cancellationToken) => Ports.ApiUrl() is { } api ? await Ports.Api.Node(api, key, cancellationToken).ConfigureAwait(true) : null;
        Whole.Panel.WhyLoader = async (key, cancellationToken) => Ports.ApiUrl() is { } api ? await Ports.Api.Lenses(api, new LensesQuery([key], Depth: null, Draft: false, MapVersion: null), cancellationToken).ConfigureAwait(true) : null;
        Whole.NodeChosen += (_, e) => _ = ChooseWholeNode(e.Value, CancellationToken.None);
        Whole.PagesRetryRequested += (_, _) => _ = ReadWhole(CancellationToken.None);
        Follow((_, cancellationToken) => FollowWhole(cancellationToken));
    }


    /// <summary>The page was shown: the whole map is read once, with no starting point.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal Task Opened(CancellationToken cancellationToken = default)
    {
        _wholeWanted = true;
        ItemOpened(itemId: null);   // the page entered bare is no longer an item's
        NoteItem(string.Empty);   // an earlier See its context is over once the page is entered again
        if (Whole.IsLoading || Concepts.IsLoading || (Concepts.HasConcepts && OpenedConceptId is not null))
        {
            return Task.CompletedTask;
        }

        if (Concepts.HasConcepts)
        {
            return ReadConceptsIfNewer(cancellationToken);
        }

        return Whole.IsReady ? ReadIfNewer(cancellationToken) : OpenFirst(cancellationToken);
    }

    /// <summary>Reads the whole map again (Retry, and when a build has ended).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal Task ReadWhole(CancellationToken cancellationToken = default) => ReadWhole(followed: null, cancellationToken);

    // followed: the stored map's version number when the read follows it, so an open concept that version lacks goes back to the list.
    private async Task ReadWhole(int? followed, CancellationToken cancellationToken)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var generation = _openGeneration;   // a first read or a repo switch that begins while this one is in flight makes its answer stale (#3697)
        _listRead++;   // an area page or concept list still being read is for the map being replaced
        Whole.Loading();
        ChangedWhole();
        try
        {
            var read = await Ports.Api.WholeMap(api, new WholeMapQuery("auto", Area: null, [], MapVersion: null, Draft: false, Repo: _repo, Concept: OpenedConceptId, Page: OpenedConceptId is null ? 1 : Whole.ConceptPage, ConceptTab: OpenedConceptId is null ? null : Whole.ConceptTab), cancellationToken).ConfigureAwait(true);
            if (generation == _openGeneration)
            {
                await DrawRead(api, read, followed, cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            if (generation == _openGeneration && Whole.IsLoading)
            {
                Whole.Failed();   // a read or a draw that threw is the failure row (Retry, Back), never "Reading the map" for good; the exception still goes to the caller
            }

            ChangedWhole();
        }
    }

    // The read's answer: an open concept the followed version lacks goes back to the list, anything else is drawn.
    private async Task DrawRead(Uri api, Result<WholeMapView> read, int? followed, CancellationToken cancellationToken)
    {
        if (followed is { } version && OpenedConceptId is { } gone && !read.IsSuccess && string.Equals(read.Error.Code, GoneConceptCode, StringComparison.Ordinal))
        {
            await LeaveGoneConcept(gone, version, cancellationToken).ConfigureAwait(true);
            return;
        }

        await DrawWhole(api, read, cancellationToken).ConfigureAwait(true);
    }

    // A Retry would ask for the same missing concept forever: the page goes back to the concepts, read again, with a line that says why.
    private async Task LeaveGoneConcept(string gone, int version, CancellationToken cancellationToken)
    {
        var name = Concepts.Find(gone)?.Name ?? gone;
        Whole.Quiet();
        CloseConcept();
        await OpenFirst(cancellationToken).ConfigureAwait(true);
        Concepts.Tell(Labels.ContextConceptGone(name, version));
    }

    // The read's answer onto the page: the map, the No map state, or the failure row.
    private async Task DrawWhole(Uri api, Result<WholeMapView> read, CancellationToken cancellationToken)
    {
        if (read.IsSuccess)
        {
            Whole.Show(read.Value);
            NoteRepos(read.Value);
            if (OpenedConceptId is { } opened)
            {
                await MarkSuggestedSeen(opened).ConfigureAwait(true);
            }

            if (_servedBuilding)
            {
                (IsBuilding, _buildJob, _servedBuilding, _notReady, NoMap) = (false, null, false, MapNotReadyView.None, false);   // the build the answer named is done: the map is here
                ChangedStates();
            }

            if (!HasStack)
            {
                Head = Whole.Head;
            }
        }
        else if (string.Equals(read.Error.Code, NoMapCode, StringComparison.Ordinal))
        {
            Whole.Quiet();
            NoteNotReady(read.Error);
            await NoteRefusal(api, read.Error, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            Whole.Failed();
        }
    }

    /// <summary>Opens one feature in full (area=): its items replace the overview, with a way back; a feature the map does not have is the existing banner.</summary>
    /// <param name="area">The area's key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal Task OpenWholeArea(string area, CancellationToken cancellationToken = default) => OpenWholeAreaPage(area, 1, cancellationToken);

    /// <summary>"~" on the map: back to the whole map from a query's answer, or from one open feature to all of them.</summary>
    internal void MapHome()
    {
        _listRead++;   // a page still being read is for the place being left
        if (!BackToWholeMap())
        {
            Whole.CloseArea();
            ChangedWhole();
        }
    }

    private void ChangedWhole()
    {
        OnChanged(nameof(Head));
        OnChanged(nameof(HasHead));
        OnChanged(nameof(HeadDetail));
        OnChanged(nameof(ShowWhole));
        OnChanged(nameof(ShowWholeStatus));
        OnChanged(nameof(ShowRulesOlder));
        OnChanged(nameof(ShowRulesRefresh));
        OnChanged(nameof(ShowStructure));
        OnChanged(nameof(SearchNote));
        OnChanged(nameof(HasSearchNote));
    }
}
