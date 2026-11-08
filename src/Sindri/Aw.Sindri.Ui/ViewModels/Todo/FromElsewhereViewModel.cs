using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The From elsewhere line under the Todo panel's rows (todo spec §3.1, §3.4, §3.5, D4): read-only counts of the PRs waiting on the owner's
/// review, the Linear issues assigned to them and the Needs you rows. Each part is a link; nothing is copied into the todo store.
/// </summary>
internal sealed class FromElsewhereViewModel(ISindriApi api) : ObservableObject
{
    private readonly ISindriApi _api = api;

    /// <summary>The line's words: the parts joined by " · ", or the loading or all-zero placeholder.</summary>
    public string Text { get; private set => Set(ref field, value); } = Labels.ElsewhereChecking;

    /// <summary>True until the reads have answered once.</summary>
    public bool IsLoading { get; private set => Set(ref field, value); } = true;

    /// <summary>The parts that are drawn, in order: Reviews, Linear, Needs you.</summary>
    public IReadOnlyList<ElsewherePartViewModel> Parts { get; private set => Set(ref field, value); } = [];

    /// <summary>The line's name for a screen reader.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Name => Labels.ElsewhereName;
#pragma warning restore CA1822, MA0041

    /// <summary>A part with a count or a Reviews or Linear link was pressed; the shell moves there.</summary>
    internal event EventHandler<ElsewherePartEventArgs>? OpenRequested;

    /// <summary>"Linear not connected" was pressed; the shell opens Settings, Connections.</summary>
    internal event EventHandler? ConnectRequested;

    /// <summary>Retry on the Reviews part was pressed; the shell reads Reviews again.</summary>
    internal event EventHandler? ReviewsRetryRequested;

    /// <summary>Takes the Reviews page's state.</summary>
    /// <param name="loading">True until its first answer.</param>
    /// <param name="waiting">PRs in its Waiting on you group.</param>
    /// <param name="failed">True when its last read failed.</param>
    internal void SetReviews(bool loading, int waiting, bool failed) => throw new NotSupportedException();

    /// <summary>Takes the Needs you count.</summary>
    /// <param name="count">Rows in Needs you.</param>
    internal void SetNeedsYou(int count) => throw new NotSupportedException();

    /// <summary>Reads the Linear issues assigned to the owner (on connect and on every event).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException($"{_api} {url} {cancellationToken.IsCancellationRequested}");

    /// <summary>A part was pressed.</summary>
    /// <param name="part">Which.</param>
    internal void Open(ElsewherePart part)
    {
        OpenRequested?.Invoke(this, new ElsewherePartEventArgs(part));
        ConnectRequested?.Invoke(this, EventArgs.Empty);
        throw new NotSupportedException();
    }

    /// <summary>Retry beside a failed part was pressed.</summary>
    /// <param name="part">Which.</param>
    /// <returns>A task that completes when a Linear read is shown; Reviews asks the shell.</returns>
    internal Task Retry(ElsewherePart part)
    {
        _ = part;
        ReviewsRetryRequested?.Invoke(this, EventArgs.Empty);
        throw new NotSupportedException();
    }
}
