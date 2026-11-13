using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The From elsewhere line under the Todo panel's rows (todo spec §3.1, §3.4, §3.5, D4): read-only counts of the PRs waiting on the owner's
/// review and the Linear issues assigned to them (Needs you is the panel right below, so the line does not count it). Each part is a link; nothing is copied into the todo store.
/// Reviews and Needs you are fed by the shell from the view models that already read them; Linear is read here, because Work's list
/// reads only while Work is shown.
/// </summary>
internal sealed class FromElsewhereViewModel(ISindriApi api) : ObservableObject
{
    internal const int LinearLimit = 100;

    private readonly ISindriApi _api = api;
    private (bool Loading, int Waiting, bool Failed) _reviews = (Loading: true, Waiting: 0, Failed: false);
    private LinearState _linear = LinearState.Loading;
    private int _assigned;
    private bool _viewsStalled;
    private bool _profileMissing;
    private Uri? _url;
    private int _reads;

    private enum LinearState
    {
        Loading,
        Ready,
        Failed,
        NotConnected,
        NotShown,   // an older Sindri has no Linear route: the line says nothing about Linear
    }

    /// <summary>The line's words: the parts joined by " · ", or the loading or all-zero placeholder.</summary>
    public string Text { get; private set => Set(ref field, value); } = Labels.ElsewhereChecking;

    /// <summary>True until the Reviews and Linear reads have answered once.</summary>
    public bool IsLoading { get; private set => Set(ref field, value); } = true;

    /// <summary>The parts that are drawn, in order: Reviews, Linear.</summary>
    public IReadOnlyList<ElsewherePartViewModel> Parts { get; private set => Set(ref field, value); } = [];

    /// <summary>The line's name for a screen reader.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Name => Labels.ElsewhereName;
#pragma warning restore CA1822, MA0041

    /// <summary>A part was pressed (Reviews or Linear connected); the shell moves there.</summary>
    internal event EventHandler<ElsewherePartEventArgs>? OpenRequested;

    /// <summary>Connect Linear was pressed; the shell opens Settings, Connections.</summary>
    internal event EventHandler? ConnectRequested;

    /// <summary>Retry on the Reviews part was pressed; the shell reads Reviews again.</summary>
    internal event EventHandler? ReviewsRetryRequested;

    /// <summary>Takes the Reviews page's state.</summary>
    /// <param name="loading">True until its first answer.</param>
    /// <param name="waiting">PRs in its Waiting on you group.</param>
    /// <param name="failed">True when its last read failed.</param>
    internal void SetReviews(bool loading, int waiting, bool failed)
    {
        _reviews = (loading, waiting, failed);
        Show();
    }

    /// <summary>Takes whether there is no profile yet: nothing is connected then, so the line offers Connect GitHub and Connect Linear instead of failed reads with Retry.</summary>
    /// <param name="missing">True on a home with no profile.</param>
    internal void SetProfileMissing(bool missing)
    {
        _profileMissing = missing;
        Show();
    }

    /// <summary>Takes whether the views are stalled: Reviews and Linear may then never answer, and the line says what is stale instead of checking forever.</summary>
    /// <param name="stalled">True while the views are stalled.</param>
    internal void SetViewsStalled(bool stalled)
    {
        _viewsStalled = stalled;
        Show();
    }

    /// <summary>Reads the Linear issues assigned to the owner (on connect and on every event).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        var read = ++_reads;
        var list = await _api.LinearIssues(url, new LinearIssueQuery("mine", Limit: LinearLimit), cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since: its answer is the one to show
        }

        (_linear, _assigned) = list switch
        {
            { IsError: true } when OlderSindri.IsNotServed(list.Error) => (LinearState.NotShown, 0),
            { IsError: true } => (LinearState.Failed, 0),
            { Value.Connected: false } => (LinearState.NotConnected, 0),
            _ => (LinearState.Ready, list.Value.Total ?? list.Value.Rows.Count),
        };
        Show();
    }

    /// <summary>A part was pressed.</summary>
    /// <param name="part">Which.</param>
    internal void Open(ElsewherePart part)
    {
        if (_profileMissing || (part == ElsewherePart.Linear && _linear == LinearState.NotConnected))
        {
            ConnectRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        OpenRequested?.Invoke(this, new ElsewherePartEventArgs(part));
    }

    /// <summary>Retry beside a failed part was pressed.</summary>
    /// <param name="part">Which.</param>
    /// <returns>A task that completes when a Linear read is shown; Reviews asks the shell.</returns>
    internal Task Retry(ElsewherePart part)
    {
        if (part == ElsewherePart.Reviews)
        {
            ReviewsRetryRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (part == ElsewherePart.Linear && _url is { } url)
        {
            return Refresh(url, CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    // Nothing is raised when the words and every part are what they were: the view rebuilds its buttons (and loses the arrow-key stop) on every change.
    private void Show()
    {
        if (_profileMissing)
        {
            var connect = new[] { Part(ElsewherePart.Reviews, Labels.ElsewhereReviewsNotConnected, "Connections", isAction: true), Part(ElsewherePart.Linear, Labels.ElsewhereLinearNotConnected, "Connections", isAction: true) };
            Apply(isLoading: false, string.Join(" · ", connect.Select(p => p.Text)), connect);
            return;
        }

        if (_reviews.Loading || _linear == LinearState.Loading)
        {
            Apply(isLoading: !_viewsStalled, _viewsStalled ? Labels.ElsewhereStale : Labels.ElsewhereChecking, []);
            return;
        }

        var parts = new[] { ReviewsPart(), LinearPart() }.OfType<ElsewherePartViewModel>().ToList();
        Apply(isLoading: false, parts.Count == 0 ? Labels.ElsewhereNothing : string.Join(" · ", parts.Select(p => p.Text)), parts);
    }

    private void Apply(bool isLoading, string text, IReadOnlyList<ElsewherePartViewModel> parts)
    {
        IsLoading = isLoading;
        if (string.Equals(Text, text, StringComparison.Ordinal) && Parts.SequenceEqual(parts))
        {
            return;
        }

        (Text, Parts) = (text, parts);
    }

    private ElsewherePartViewModel? ReviewsPart() =>
        _reviews.Failed ? Part(ElsewherePart.Reviews, Labels.ElsewhereReviewsFailed, "Reviews", retryName: "Reviews")
        : _reviews.Waiting > 0 ? Part(ElsewherePart.Reviews, Labels.ElsewhereReviews(_reviews.Waiting), "Reviews")
        : null;

    private ElsewherePartViewModel? LinearPart() => _linear switch
    {
        LinearState.Failed => Part(ElsewherePart.Linear, Labels.ElsewhereLinearFailed, "Linear", retryName: "Linear"),
        LinearState.NotConnected => Part(ElsewherePart.Linear, Labels.ElsewhereLinearNotConnected, "Connections", isAction: true),
        LinearState.Ready when _assigned > 0 => Part(ElsewherePart.Linear, Labels.ElsewhereLinear(_assigned), "Linear"),
        LinearState.Loading or LinearState.Ready or LinearState.NotShown => null,
        _ => throw new InvalidOperationException(_linear.ToString()),
    };

    private static ElsewherePartViewModel Part(ElsewherePart part, string text, string target, string? retryName = null, bool isAction = false) =>
        new(part, text, Labels.ElsewhereOpens(target, text), retryName is not null, retryName is null ? string.Empty : Labels.ElsewhereRetryName(retryName), isAction);
}
