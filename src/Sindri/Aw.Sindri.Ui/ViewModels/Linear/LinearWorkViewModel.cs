#pragma warning disable CA1822, MA0041, MA0182, CS0067 // why: the red stubs read nothing yet

using System.Collections.ObjectModel;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>The Work page's Linear view (Linear hub spec §3): the switch, the list, the filters and the since-you-looked strip.</summary>
internal sealed class LinearWorkViewModel : ObservableObject
{
    internal LinearWorkViewModel(Api.ISindriApi api, Preferences preferences, TimeProvider clock)
    {
        throw new NotSupportedException();
    }

    /// <summary>True when the Linear tab is picked.</summary>
    public bool IsLinear => throw new NotSupportedException();

    /// <summary>True when the Sindri tab is picked.</summary>
    public bool IsSindri => throw new NotSupportedException();

    /// <summary>The Linear tab's text.</summary>
    public string LinearTabText => throw new NotSupportedException();

    /// <summary>The Linear tab's accessible name.</summary>
    public string LinearTabName => throw new NotSupportedException();

    /// <summary>The header button's words.</summary>
    public string CreateText => throw new NotSupportedException();

    /// <summary>Picks a tab and remembers the pick.</summary>
    /// <param name="linear">True for Linear.</param>
    internal void Choose(bool linear) => throw new NotSupportedException();

    /// <summary>`L`: the other tab.</summary>
    internal void Toggle() => throw new NotSupportedException();

    /// <summary>A key on a tab: Left, Right, Home or End.</summary>
    /// <param name="key">The key.</param>
    /// <returns>True when the key moved the pick or kept it.</returns>
    internal bool MoveTab(string key) => throw new NotSupportedException();

    /// <summary>Reads the remembered pick and the strip's marker.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when they are applied.</returns>
    internal Task Restore(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>The Work page was shown or hidden.</summary>
    /// <param name="shown">True while Work is the page.</param>
    internal void SetShown(bool shown) => throw new NotSupportedException();

    /// <summary>Re-reads on connect and on every event while Work is shown.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the reads are done.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>The latest reads started.</summary>
    internal Task Reading => throw new NotSupportedException();

    /// <summary>True until the first answer.</summary>
    public bool IsLoading => throw new NotSupportedException();

    /// <summary>Assigned to you.</summary>
    public LinearGroupViewModel Mine => throw new NotSupportedException();

    /// <summary>Your teams.</summary>
    public LinearGroupViewModel Teams => throw new NotSupportedException();

    /// <summary>The two groups in order.</summary>
    public ObservableCollection<LinearGroupViewModel> Groups => throw new NotSupportedException();

    /// <summary>The seven drop-downs.</summary>
    public ObservableCollection<LinearFilterViewModel> Filters => throw new NotSupportedException();

    /// <summary>A filter by key.</summary>
    /// <param name="key">team, project, cycle, state, assignee, priority or label.</param>
    /// <returns>The filter.</returns>
    internal LinearFilterViewModel Filter(string key) => throw new NotSupportedException();

    /// <summary>True when any filter is on.</summary>
    public bool HasFilters => throw new NotSupportedException();

    /// <summary>Turns every filter off and reloads.</summary>
    internal void ClearFilters() => throw new NotSupportedException();

    /// <summary>True when a group has another page.</summary>
    public bool HasMore => throw new NotSupportedException();

    /// <summary>Loads the next page of the first group that has one.</summary>
    /// <returns>A task that completes when the rows are added.</returns>
    internal Task ShowMore() => throw new NotSupportedException();

    /// <summary>Focus should move to a row.</summary>
    internal event EventHandler<FocusRequestedEventArgs>? FocusRequested;

    /// <summary>True when no Linear source is set up.</summary>
    public bool ShowsConnect => throw new NotSupportedException();

    /// <summary>The Connect Linear button was pressed: the shell opens Settings.</summary>
    internal event EventHandler? ConnectRequested;

    /// <summary>Asks the shell to open Settings.</summary>
    internal void RequestConnect() => throw new NotSupportedException();

    /// <summary>True while the viewer step has not run and nothing is assigned yet.</summary>
    public bool ShowsFinding => throw new NotSupportedException();

    /// <summary>True when filters are on and nothing matches.</summary>
    public bool ShowsNoMatch => throw new NotSupportedException();

    /// <summary>The line while the feed re-reads; empty otherwise.</summary>
    public string UpdatingText => throw new NotSupportedException();

    /// <summary>The banner while the feed fails; empty otherwise.</summary>
    public string FeedText => throw new NotSupportedException();

    /// <summary>True when the last read could not reach the API.</summary>
    public bool IsUnreachable => throw new NotSupportedException();

    /// <summary>"as of 10:05" while unreachable and rows are shown.</summary>
    public string AsOfText => throw new NotSupportedException();

    /// <summary>True when the strip shows.</summary>
    public bool ShowsStrip => throw new NotSupportedException();

    /// <summary>How many changes the strip holds.</summary>
    public int NewCount => throw new NotSupportedException();

    /// <summary>The strip's region name.</summary>
    public string StripName => throw new NotSupportedException();

    /// <summary>The strip's rows.</summary>
    public ObservableCollection<LinearSinceRowViewModel> StripRows => throw new NotSupportedException();

    /// <summary>True when the changes could not be read.</summary>
    public bool StripFailed => throw new NotSupportedException();

    /// <summary>Dismiss: the one thing that stores the marker.</summary>
    /// <returns>A task that completes when the marker is written.</returns>
    internal Task Dismiss() => throw new NotSupportedException();

    /// <summary>Retry: reads the changes again.</summary>
    /// <returns>A task that completes when the read is done.</returns>
    internal Task RetryStrip() => throw new NotSupportedException();

    /// <summary>A row asked for its issue.</summary>
    internal event EventHandler<LinearIssueRequestedEventArgs>? IssueRequested;

    /// <summary>Asks the shell to open an issue.</summary>
    /// <param name="key">The issue key.</param>
    internal void RequestIssue(string key) => throw new NotSupportedException();
}
