using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The control plane's health (S1b's status card, the Health page in U0) and the connection behind the shell's pre-API
/// state (UI spec §4.1). It finds the control plane through its endpoint record, checks the record against /sindri/ping
/// (a stale record reads as not running), shows /sindri/status, and refreshes the status after each event on
/// /sindri/events. When the stream ends it looks again. Every await resumes on the caller's context (the UI thread in the
/// app), so property changes reach the bindings there; the freshness ticker posts to that context too.
/// </summary>
/// <param name="records">Where the control plane recorded its endpoint.</param>
/// <param name="api">Its API.</param>
/// <param name="clock">The clock for retry waits and freshness.</param>
/// <param name="clipboard">Where the catalog's controls copy a CLI.</param>
internal sealed partial class HealthViewModel(IEndpointRecordSource records, ISindriApi api, TimeProvider clock, IClipboard clipboard) : ObservableObject
{
    /// <summary>How long to wait before looking for the control plane again.</summary>
    internal static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(2);

    /// <summary>How long without an event, a status or an answered ping before the views are stale (§4.1, API §2.4).</summary>
    internal static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);

    /// <summary>How long a refresh is in flight before "Sending…" shows (§4.1 "sending").</summary>
    internal static readonly TimeSpan SendingAfter = TimeSpan.FromMilliseconds(300);

    /// <summary>How often the follow pings the control plane it follows, well inside <see cref="StaleAfter"/>.</summary>
    internal static readonly TimeSpan PingEvery = TimeSpan.FromSeconds(10);

    /// <summary>The shortest time between two re-reads of a view after events (§9.2: one refresh per view per 250 ms).</summary>
    internal static readonly TimeSpan RefreshEvery = TimeSpan.FromMilliseconds(250);

    /// <summary>How many more times a view that lags an instance change's state is read (after the first read), each after twice the wait before: <see cref="InstanceBackoff"/>, then double, then double.</summary>
    internal const int InstanceRetries = 3;

    /// <summary>The wait before the first retry of a lagging view.</summary>
    internal static readonly TimeSpan InstanceBackoff = TimeSpan.FromMilliseconds(200);

    // How often "updated Ns ago" is re-worded.
    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(1);

    // The ticker runs on the clock's thread; in tests (no UI context) it runs inline, so state changes take this lock.
    private readonly Lock _gate = new();
    private DateTimeOffset _heard;
    private Uri? _url;
    private int _failedRefreshes;
    private int _refreshes;
    private readonly UiContext _ui = new();
    private HealthView? _health;
    private DateTimeOffset? _projectedAt;

    /// <summary>The app's copy, for the view.</summary>
    public ShellText Text { get; } = ShellText.Instance;

    /// <summary>The pre-API state (§4.1).</summary>
    public ShellState State { get; private set; } = ShellState.Connecting;

    /// <summary>The health pill's parts.</summary>
    public HealthPill Pill { get; } = new();

    /// <summary>The command catalog as controls (read on connect and after a reconnect).</summary>
    public CatalogViewModel Catalog { get; } = new(clipboard, clock);

    /// <summary>The stalled and rebuilding banners (§4.1), from the services view's asOf.</summary>
    public ViewsBanner Banner { get; } = new();

    /// <summary>The status as labelled values: Home's strip and the Health page's rows.</summary>
    public HealthDetail Detail { get; } = new();

    /// <summary>Home's health strip (§4.2, §4.8): the status's fields in the health line's order, or the state in words.</summary>
    public string HealthLine { get; private set => Set(ref field, value); } = Labels.Connecting;

    /// <summary>The state in one word: Connecting…, Running or Not running.</summary>
    public string Headline { get; private set => Set(ref field, value); } = Labels.Connecting;

    /// <summary>Why it isn't running, the two versions, or the unreadable answer's code and message; empty otherwise.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>True while a control plane answers.</summary>
    public bool IsRunning { get; private set => Set(ref field, value); }

    /// <summary>True once it's known that none does.</summary>
    public bool IsNotRunning { get; private set => Set(ref field, value); }

    /// <summary>Refresh takes a click: false while one is in flight (§4.1 "sending": disabled, its label kept; a second click does nothing).</summary>
    public bool CanRefresh { get; private set => Set(ref field, value); } = true;

    /// <summary>
    /// A refresh has been in flight for 300 ms: "Sending…" is announced politely (§4.1). It is the live region's gate, so
    /// it changes before the announcement does: on before the text becomes "Sending…", off before it goes back to rest.
    /// </summary>
    public bool IsSending
    {
        get;
        private set
        {
            Set(ref field, value);
            SendingAnnouncement = value ? Labels.Sending : Labels.Refresh;
        }
    }

    /// <summary>The newest event seen on the stream.</summary>
    public string LastEvent { get; private set => Set(ref field, value); } = "none yet";

    /// <summary>Follows the control plane until cancelled, re-wording the freshness every second.</summary>
    /// <param name="cancellationToken">Stops following.</param>
    /// <returns>A task that ends when cancelled.</returns>
    internal async Task Run(CancellationToken cancellationToken)
    {
        var ticker = clock.CreateTimer(_ => _ui.PostOrInline(Age), state: null, _tick, _tick);
        await using (ticker.ConfigureAwait(true))
        {
            await new ControlPlaneFollower(records, api, clock, this).Run(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>An event on the stream: the newest event, and the views are fresh.</summary>
    /// <param name="e">The event.</param>
    internal void Event(EventView e)
    {
        LastEvent = StatusText.Event(e);
        Heard();
    }

    /// <summary>The followed control plane answered a ping: it is there, so the views are fresh (§4.1).</summary>
    internal void Answered() => Heard();

    /// <summary>The live profile's hash from the last status, when it loads; the typed approval is tied to it.</summary>
    internal string? LiveProfile { get; private set; }

    /// <summary>A status: the card's words, or a version mismatch.</summary>
    /// <param name="url">Where the API answered.</param>
    /// <param name="status">The status.</param>
    internal void Show(Uri url, StatusView status)
    {
        _ = Interlocked.Increment(ref _decisions);
        if (status.ApiVersion is { } version && version != SindriApiClient.AppApiVersion)
        {
            Mismatch(version);
            return;
        }

        if (!status.Running)
        {
            _ = NotRunning(Labels.NoLease);
        }

        _url = url;
        (_health, _projectedAt) = (status.Health, status.Views?.ProjectedAt);
        Sessions = status.Sessions;
        Detail.Show(url, status);
        Api = url.Authority;
        Lease = StatusText.Lease(status);
        ProfileState = StatusText.ProfileState(status.Profile);
        ProfileDetail = StatusText.ProfileDetail(status.Profile);
        ProfileFix = StatusText.ProfileFix(status.Profile);
        LiveProfile = status.Profile.Live;
        Services = [.. status.Services.Select(StatusText.Service)];
        if (status.Running)
        {
            lock (_gate)
            {
                Headline = Labels.Running;
                HealthLine = StatusText.HealthLine(status);
                IsRunning = true;
                IsNotRunning = false;
                Become(ShellState.Normal, string.Empty);
            }

            Heard();
        }
    }

    /// <summary>The catalog as the API at <paramref name="url"/> served it; the stalled banner's Rebuild views follows it.</summary>
    /// <param name="url">Where the API answered.</param>
    /// <param name="catalog">The catalog.</param>
    internal void ShowCatalog(Uri url, CommandList catalog)
    {
        Catalog.Show(url, api, catalog);
        ReconnectServiceControls();
    }

    /// <summary>The services view (API P2) for the banners; null when it isn't served or failed, which hides them.</summary>
    /// <param name="view">The view, or null.</param>
    internal void ShowViews(ServiceList? view)
    {
        Banner.Show(view, Catalog.For("RebuildViews"));
        ShowServices(view);
    }

    /// <summary>
    /// A failed call: SND-UI-002 (nothing answered) is Not running, led by its sentence with the address on its own line
    /// (I7). Anything else answered: SND-UI-003 is an answer this build can't read, and a refusal (Sindri's code after the
    /// client's one retry, e.g. SND-AUTH-001 for a missing or stale token) is Sindri's code and detail as they are (API
    /// §3.6); both are the Unreadable banner, the last data kept (orchestrator decision, PR #184). The decision keys off
    /// the code, never the detail sentence. The app keeps looking every 2 s either way.
    /// </summary>
    /// <param name="error">The call's error.</param>
    /// <param name="api">The API the call went to.</param>
    /// <returns>Null: no API to follow.</returns>
    internal Uri? Failed(Error error, Uri api) =>
        string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal)
            ? NotRunning(Labels.NothingAnswered, api.Authority)
            : Unreadable(error);

    /// <summary>Not running, and why.</summary>
    /// <param name="reason">The reason in words.</param>
    /// <param name="detail">The reason's detail, shown on its own line (I7): the record's path shortened, the address; or null.</param>
    /// <returns>Null: no API to follow.</returns>
    internal Uri? NotRunning(string reason, string? detail = null)
    {
        lock (_gate)
        {
            _decisions++;
            Headline = Labels.NotRunning;
            HealthLine = Labels.NotRunning;
            ReasonDetail = detail ?? string.Empty;
            IsRunning = false;
            IsNotRunning = true;
            Become(ShellState.NotRunning, reason);
            Word();
        }

        return null;
    }

    // An event, a status or an answered ping: the views are fresh again.
    private void Heard()
    {
        lock (_gate)
        {
            _heard = clock.GetUtcNow();
            _failedRefreshes = 0;
            if (State is ShellState.Normal or ShellState.Stale)
            {
                Become(ShellState.Normal, Reason);
                Word();
            }
        }
    }

    // The ticker: "updated Ns ago", and after StaleAfter without an event, a status or an answered ping the word stale.
    private void Age()
    {
        lock (_gate)
        {
            if (State is not (ShellState.Normal or ShellState.Stale))
            {
                return;
            }

            if (clock.GetUtcNow() - _heard >= StaleAfter)
            {
                Become(ShellState.Stale, Reason);
            }

            Word();
        }
    }

    // The pill for the state now (under the gate): its age counts from the last event, status or answered ping.
    private void Word()
    {
        Pill.Show(State, clock.GetUtcNow() - (_projectedAt ?? _heard), _failedRefreshes >= NotRespondingAfter, _health);
        StripName = Pill.IsDataStale ? HealthLine + " · " + Labels.Stale : HealthLine;
        Sentence = SentenceFor(State, _health is not null, Pill.IsHealthy, Pill.Problems, HasProfileFix);
        ShowSystemState();
    }

    // §4.1 Stale: "After 3 failed refreshes: Sindri not responding: run sindri doctor".
    private const int NotRespondingAfter = 3;
}
