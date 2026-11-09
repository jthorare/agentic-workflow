using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Connections;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>
/// The Connections page (PR 18a): the names workflows and agents sign in by, each with its kind, a status chip (connected, needs sign-in, error), why it is not connected and
/// when its token was last refreshed; the row's actions are the MCP tools' (list, get through the sheet, add, test, sign in again, remove). The page reads while it is the page shown and
/// on every event, and keeps its rows by name across reads. A connection leaves the list only after the API confirms its removal.
/// </summary>
internal sealed class ConnectionsPageViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, ConnectionRowViewModel> _rows = new(StringComparer.Ordinal);
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private readonly ConnectionActions _actions;
    private int _reads;
    private CancellationTokenSource? _shown;
    private Uri? _url;

    /// <summary>The page over its ports.</summary>
    /// <param name="api">The API.</param>
    /// <param name="launcher">Opens a consent page.</param>
    /// <param name="clock">The clock the ages and the sign-in poll run on.</param>
    internal ConnectionsPageViewModel(ISindriApi api, ILauncher launcher, TimeProvider clock)
    {
        (_api, _clock) = (api, clock);
        _actions = new ConnectionActions(api, () => _url, launcher, clock);
        Add = new AddConnectionViewModel(_actions, ReadAgain);
    }

    /// <summary>Raised when a row asks to open its connection's sheet; the shell opens it.</summary>
    internal event EventHandler<ConnectionOpenRequestedEventArgs>? OpenRequested;

    /// <summary>The page's copy.</summary>
    public ConnectionsText Text { get; } = ConnectionsText.Instance;

    /// <summary>The Add connection form.</summary>
    public AddConnectionViewModel Add { get; }

    /// <summary>The connections, as the API orders them.</summary>
    public ObservableCollection<ConnectionRowViewModel> Rows { get; } = [];

    /// <summary>"3 connections · 1 needs you"; empty until the first list.</summary>
    public string Head { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The head's accessible name.</summary>
    public string HeadName => Head.Length > 0 ? Labels.ConnectionsPage + ", " + Head : Labels.ConnectionsPage;

    /// <summary>True until the first answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); } = true;

    /// <summary>True when the list is empty.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>The sentence for an empty list.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the answer can't be read by this build.</summary>
    public bool IsUnreadable { get; private set => Set(ref field, value); }

    /// <summary>True while the rows shown are the last good ones (a read failed).</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>True when the first read failed and there are no rows to show.</summary>
    public bool IsFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when a retry is offered.</summary>
    public bool ShowRetry { get; private set => Set(ref field, value); }

    /// <summary>The catch-up read started by the last show.</summary>
    internal Task CatchUp { get; private set; } = Task.CompletedTask;

    /// <summary>How many lists this page has drawn (a test waits on it).</summary>
    internal int Drawn { get; private set; }

    /// <summary>True while the page is the one shown.</summary>
    internal bool IsActive { get; private set; }

    /// <summary>Asks the shell to open a connection's sheet.</summary>
    /// <param name="link">The connection's link.</param>
    internal void RequestOpen(DeepLink link) => OpenRequested?.Invoke(this, new ConnectionOpenRequestedEventArgs(link));

    /// <summary>Reads again after a failed read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the list is shown.</returns>
    internal Task Retry(CancellationToken cancellationToken) => _url is { } url ? Read(url, cancellationToken) : Task.CompletedTask;

    /// <summary>Re-reads the list (on connect and on every event). While the page is not the one shown it only remembers where the API is.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return IsActive ? Read(url, cancellationToken) : Task.CompletedTask;
    }

    /// <summary>The page was shown or hidden (the shell's page changed). Becoming the page shown reads at once.</summary>
    /// <param name="shown">True while the Connections page is the page.</param>
    internal void SetShown(bool shown)
    {
        var was = IsActive;
        IsActive = shown;
        if (shown && !was && _url is { } url)
        {
            foreach (var row in _rows.Values)
            {
                row.Age();
            }

            _shown = new CancellationTokenSource();
            CatchUp = CatchUpRead(url, _shown.Token);
        }
        else if (!shown)
        {
            _reads++;   // a read already on its way is not wanted
            Dispose();
        }
    }

    /// <summary>Cancels the read the page started when it was shown.</summary>
    public void Dispose()
    {
        _shown?.Cancel();
        _shown?.Dispose();
        _shown = null;
    }

    // The read made when the page is shown: a leave cancels it, any other failure is drawn as a failed read.
    private async Task CatchUpRead(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            await Read(url, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // the page was left
        }
        catch (HttpRequestException e)
        {
            Fail(SindriUiErrors.Unreachable.With(e.Message));
        }
    }

    // The form asks for a read after a write: the page reads at once, shown or not, so the new row is there when the form says it is.
    private Task ReadAgain() => _url is { } url ? Read(url, CancellationToken.None) : Task.CompletedTask;

    private async Task Read(Uri url, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        var list = await _api.ListConnections(url, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since, or the page was left: its answer is the one to show
        }

        if (list.IsSuccess)
        {
            Show(list.Value);
            return;
        }

        Fail(list.Error);
    }

    private void Fail(Error error)
    {
        var unreadable = string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal);
        (IsReading, IsUnreadable, IsStale) = (false, unreadable, true);   // the last good rows stay, marked stale (screens §4.1)
        IsFailed = !unreadable && Rows.Count == 0;
        ShowRetry = !unreadable;
        FailedText = ShowRetry ? Labels.ConnectionsFailed(error) : string.Empty;
    }

    private void Show(ConnectionList list)
    {
        Drawn++;
        (IsReading, IsUnreadable, IsStale, IsFailed, FailedText, ShowRetry) = (false, false, false, false, string.Empty, false);
        var rows = list.Connections.Select(Row).ToList();
        var present = new HashSet<string>(list.Connections.Select(c => c.Name), StringComparer.Ordinal);
        foreach (var gone in _rows.Keys.Where(k => !present.Contains(k)).ToList())
        {
            _ = _rows.Remove(gone);
        }

        Patch.To(Rows, rows);
        Head = Labels.ConnectionsHead(rows.Count, rows.Count(r => r.IsProblem));
        (IsEmpty, EmptyText) = (rows.Count == 0, rows.Count == 0 ? Labels.ConnectionsEmpty : string.Empty);
        OnChanged(nameof(HeadName));
    }

    private ConnectionRowViewModel Row(ConnectionView served)
    {
        if (_rows.TryGetValue(served.Name, out var kept))
        {
            kept.Update(served);
            return kept;
        }

        var row = new ConnectionRowViewModel(served, _actions, _clock);
        row.Removed += OnRemoved;
        return _rows[served.Name] = row;
    }

    // The API confirmed a removal: the row leaves now, without waiting for the next read.
    private void OnRemoved(object? sender, EventArgs e)
    {
        if (sender is not ConnectionRowViewModel gone)
        {
            return;
        }

        _ = _rows.Remove(gone.Name);
        _ = Rows.Remove(gone);
        Head = Labels.ConnectionsHead(Rows.Count, Rows.Count(r => r.IsProblem));
        (IsEmpty, EmptyText) = (Rows.Count == 0, Rows.Count == 0 ? Labels.ConnectionsEmpty : string.Empty);
        OnChanged(nameof(HeadName));
    }
}
