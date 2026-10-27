using System.Globalization;
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
        Whole.HomeRequested += (_, _) => MapHome();
        Whole.Panel.Launcher = Ports.Launcher ?? NullLauncher.Instance;
        Whole.Panel.Loader = async (key, cancellationToken) => Ports.ApiUrl() is { } api ? await Ports.Api.Node(api, key, cancellationToken).ConfigureAwait(true) : null;
        Whole.NodeChosen += (_, e) => _ = ChooseWholeNode(e.Value, CancellationToken.None);
        Ports.Follow((_, cancellationToken) => FollowWhole(cancellationToken));
    }

    // After every event: a map not read yet is read; one that is drawn is read again when the stored map moved to another version (an import, a promote).
    private Task FollowWhole(CancellationToken cancellationToken)
    {
        if (!_wholeWanted || Whole.IsLoading || Whole.IsFailed || Concepts.IsOnStage)
        {
            return Task.CompletedTask;
        }

        return Whole.IsReady ? ReadIfNewer(cancellationToken) : _conceptsRead ? ReadWhole(cancellationToken) : OpenFirst(cancellationToken);
    }

    // The cheap status says which version is current; the map is read again only when that is not the one drawn. A person's own answer (a query, an open feature) is left alone until they are back on the whole map.
    private async Task ReadIfNewer(CancellationToken cancellationToken)
    {
        if (HasStack || Whole.IsZoomed || Whole.IsAreaOpen || Whole.Answer is not { } drawn || Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var status = await Ports.Api.MapStatus(api, anchor: null, cancellationToken).ConfigureAwait(true);
        if (!status.IsSuccess)
        {
            return;
        }

        var number = status.Value.Version.Number;
        if (number == _rereadNumber || string.Equals(drawn.MapVersion, string.Create(CultureInfo.InvariantCulture, $"v{number}"), StringComparison.Ordinal))
        {
            return;
        }

        _rereadNumber = number;   // a repo whose numbers differ from the answer's is read once per version, never in a loop
        await ReadWhole(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>The page was shown: the whole map is read once, with no starting point.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal Task Opened(CancellationToken cancellationToken = default)
    {
        _wholeWanted = true;
        if (Whole.IsLoading || Concepts.IsOnStage)
        {
            return Task.CompletedTask;
        }

        return Whole.IsReady ? ReadIfNewer(cancellationToken) : OpenFirst(cancellationToken);
    }

    /// <summary>Reads the whole map again (Retry, and when a build has ended).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal async Task ReadWhole(CancellationToken cancellationToken = default)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        Whole.Loading();
        ChangedWhole();
        try
        {
            var read = await Ports.Api.WholeMap(api, new WholeMapQuery("auto", Area: null, [], MapVersion: null, Draft: false, Repo: _repo, Concept: OpenedConceptId), cancellationToken).ConfigureAwait(true);
            await DrawWhole(api, read, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            if (Whole.IsLoading)
            {
                Whole.Failed();   // a read or a draw that threw is the failure row (Retry, Back), never "Reading the map" for good; the exception still goes to the caller
            }

            ChangedWhole();
        }
    }

    // The read's answer onto the page: the map, the No map state, or the failure row.
    private async Task DrawWhole(Uri api, Result<WholeMapView> read, CancellationToken cancellationToken)
    {
        if (read.IsSuccess)
        {
            Whole.Show(read.Value);
            NoteRepos(read.Value);
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
    internal async Task OpenWholeArea(string area, CancellationToken cancellationToken = default)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var read = await Ports.Api.WholeMap(api, new WholeMapQuery("nodes", area, [], MapVersion: null, Draft: false, Repo: _repo, Concept: OpenedConceptId), cancellationToken).ConfigureAwait(true);
        if (read.IsSuccess)
        {
            Whole.ShowArea(read.Value);
            Problem = string.Empty;
        }
        else
        {
            Problem = read.Error.Message;
        }

        OnChanged(nameof(Problem));
        OnChanged(nameof(HasProblem));
        ChangedWhole();
    }

    /// <summary>"~" on the map: back to the whole map from a query's answer, or from one open feature to all of them.</summary>
    internal void MapHome()
    {
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
        OnChanged(nameof(ShowWhole));
        OnChanged(nameof(ShowWholeStatus));
        OnChanged(nameof(ShowRulesOlder));
        OnChanged(nameof(ShowRulesRefresh));
        OnChanged(nameof(SearchNote));
        OnChanged(nameof(HasSearchNote));
    }
}
