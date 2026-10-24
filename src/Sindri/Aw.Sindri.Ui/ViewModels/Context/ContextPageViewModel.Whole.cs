using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The page's first state (<see cref="ContextPageViewModel"/>, continued; whole-map spec §3.1): the whole map is read when the page is first shown, with no typing.</summary>
internal sealed partial class ContextPageViewModel
{
    private bool _wholeWanted;

    /// <summary>The whole map the page opens on.</summary>
    public WholeMapViewModel Whole { get; } = new();

    /// <summary>Whether the whole map is on the stage: it is read, and neither a query's neighbourhood, the No map state nor a running build covers it.</summary>
    public bool ShowWhole => ShowMap && Whole.IsReady && !NoMap && !IsBuilding;

    /// <summary>Whether the quiet loading row or the failure row shows over the stage.</summary>
    public bool ShowWholeStatus => ShowMap && !HasStack && !NoMap && !IsBuilding && (Whole.IsLoading || Whole.IsFailed);

    /// <summary>The note by the search box when nothing on the map matches.</summary>
    public string SearchNote => Whole.NoMatches ? Labels.ContextSearchNoMatches : string.Empty;

    /// <summary>Whether the note shows.</summary>
    public bool HasSearchNote => Whole.NoMatches;

    /// <summary>The clear button's words.</summary>
    public string SearchClearText { get; } = Labels.ContextSearchClear;

    // Registered once: whenever the control plane is reachable and the page was shown, a map that is not read yet is read (the first open may have come before the connection).
    private void InitWhole()
    {
        Whole.AreaOpenRequested += (_, e) => _ = OpenWholeArea(e.Value, CancellationToken.None);
        Whole.NodeChosen += (_, e) => _ = ChooseWholeNode(e.Value, CancellationToken.None);
        Ports.Follow((_, cancellationToken) => _wholeWanted && !Whole.IsReady && !Whole.IsLoading && !Whole.IsFailed ? ReadWhole(cancellationToken) : Task.CompletedTask);
    }

    /// <summary>The page was shown: the whole map is read once, with no starting point.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal Task Opened(CancellationToken cancellationToken = default)
    {
        _wholeWanted = true;
        return Whole.IsReady || Whole.IsLoading ? Task.CompletedTask : ReadWhole(cancellationToken);
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
        var read = await Ports.Api.WholeMap(api, new WholeMapQuery("auto", Area: null, [], PinnedVersion, Draft: false), cancellationToken).ConfigureAwait(true);
        if (read.IsSuccess)
        {
            Whole.Show(read.Value);
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

        ChangedWhole();
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

        var read = await Ports.Api.WholeMap(api, new WholeMapQuery("nodes", area, [], PinnedVersion, Draft: false), cancellationToken).ConfigureAwait(true);
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

    private void ChangedWhole()
    {
        OnChanged(nameof(Head));
        OnChanged(nameof(HasHead));
        OnChanged(nameof(ShowWhole));
        OnChanged(nameof(ShowWholeStatus));
        OnChanged(nameof(SearchNote));
        OnChanged(nameof(HasSearchNote));
    }
}
