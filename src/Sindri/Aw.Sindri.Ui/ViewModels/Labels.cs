using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// The app's own copy (UI spec §12.4): labels, headings and the sentences no API serves at that moment. Everything else
/// the app shows is the API's text as it is. One file, shared by every head (heads get theirs through the app, e.g.
/// <see cref="Platform.TrayView"/>). Later phases add theirs here, keyed by view name and action id.
/// </summary>
internal static partial class Labels
{
    // Pages and the top bar (§3.2, §3.3).

    /// <summary>The Home page.</summary>
    internal const string Home = "Home";

    /// <summary>The Queue page.</summary>
    internal const string Queue = "Queue";

    /// <summary>The Decisions page.</summary>
    internal const string Decisions = "Decisions";

    /// <summary>The Services page.</summary>
    internal const string Services = "Services";

    /// <summary>The Workflows page.</summary>
    internal const string Workflows = "Workflows";

    /// <summary>The Work page.</summary>
    internal const string Work = "Work";

    /// <summary>The Recipes page.</summary>
    internal const string Recipes = "Workflow types";

    /// <summary>The Context page.</summary>
    internal const string Context = "Context";

    /// <summary>The System page.</summary>
    internal const string System = "System";

    /// <summary>The Health page.</summary>
    internal const string Health = "Health";

    /// <summary>The Settings page.</summary>
    internal const string Settings = "Settings";

    /// <summary>The menu of views the API doesn't serve yet.</summary>
    internal const string Later = "Later";

    /// <summary>A later view (Agents, Repos, Today, Automatic work).</summary>
    internal const string Agents = "Agents";

    /// <summary>A later view.</summary>
    internal const string Repos = "Repos";

    /// <summary>A later view.</summary>
    internal const string Today = "Today";

    /// <summary>A later view.</summary>
    internal const string AutomaticWork = "Automatic work";

    /// <summary>What a view, panel or menu entry the API doesn't serve yet says (U12, §10.2).</summary>
    internal const string NotServedYet = "coming soon";

    /// <summary>The ⌘K field's placeholder (functional from U1).</summary>
    internal const string Search = "Search or run a command";

    // The shell's chrome (§3.2; U0 Unit A). Eyebrows are written in capitals (Avalonia has no text-transform).

    /// <summary>The brand mark's name.</summary>
    internal const string Sindri = "Sindri";

    /// <summary>The ⌘K field's key chip.</summary>
    internal const string SearchKey = "⌘K";

    /// <summary>The ⌘K chip's letter, drawn after the ⌘ icon.</summary>
    internal const string SearchKeyLetter = "K";

    /// <summary>Home's Needs you panel.</summary>
    internal const string NeedsYouPanel = "NEEDS YOU";

    /// <summary>Home's Running and With you panel.</summary>
    internal const string RunningPanel = "RUNNING · WITH YOU";

    /// <summary>Home's Queued panel.</summary>
    internal const string QueuedPanel = "QUEUED";

    /// <summary>Home's health strip.</summary>
    internal const string HealthPanel = "SINDRI HEALTH";

    /// <summary>The Health page's process card.</summary>
    internal const string ControlPlaneCard = "Control plane";

    /// <summary>The Health page's lease card.</summary>
    internal const string LeaseCard = "Lease";

    /// <summary>The Health page's profile card.</summary>
    internal const string ProfileCard = "Profile";

    /// <summary>The Health page's services card.</summary>
    internal const string ServicesCard = "Services";

    /// <summary>The Health page's last-event card.</summary>
    internal const string LastEventCard = "Last event";

    /// <summary>The services card with none scheduled.</summary>
    internal const string NoServices = "none scheduled (the approved profile names no services)";

    /// <summary>The not-running panel's fix prompt.</summary>
    internal const string StartIt = "Start it:";

    /// <summary>The not-running panel's last line: how often the app looks for the control plane again (HealthViewModel.RetryEvery).</summary>
    internal const string LooksAgain = "Looks again every 2 s";

    /// <summary>The unreadable banner's fix prompt.</summary>
    internal const string CheckIt = "Check it:";

    /// <summary>The detail sheet's close button.</summary>
    internal const string CloseSheet = "Close";

    /// <summary>The sheet's dismiss button's accessible name: what it closes, distinct from "Close item ITM-…".</summary>
    internal const string CloseSheetName = "Close sheet";

    /// <summary>The detail sheet's back button, when it covers the page (below 1100 px).</summary>
    internal const string Back = "Back";

    /// <summary>The closed sheet's edge tab, before its subject.</summary>
    internal const string Reopen = "Reopen";

    /// <summary>The edge tab's word for a sheet whose title hasn't loaded (or has none): the kind, never its address.</summary>
    internal const string EdgeTabItem = "Item";
    internal const string EdgeTabWorkflowRun = "Workflow run";
    internal const string EdgeTabNeedsYou = "Needs you";
    internal const string EdgeTabDecision = "Decision";
    internal const string EdgeTabJob = "Job";
    internal const string EdgeTabSession = "Session";

    /// <summary>The detail sheet's resize grip.</summary>
    internal const string Sheet = "Detail sheet width";

    /// <summary>The top bar's navigation.</summary>
    internal const string Pages = "Pages";

    // The states before the API answers (§4.1).

    /// <summary>First load, before /status answers.</summary>
    internal const string Connecting = "Connecting…";

    /// <summary>The control plane answers and holds the lease.</summary>
    internal const string Running = "Running";

    /// <summary>No usable control.json, or ping fails (SND-UI-001/002).</summary>
    internal const string NotRunning = "Not running";

    /// <summary>The command that starts Sindri.</summary>
    internal const string StartFix = "sindri";

    /// <summary>The health check to run.</summary>
    internal const string DoctorFix = "sindri doctor";

    /// <summary>The version-mismatch panel's sentence (API §9).</summary>
    internal const string VersionMismatch = "Sindri and the app are different versions";

    /// <summary>The version-mismatch panel's fix.</summary>
    internal const string VersionMismatchFix = "quit both and run sindri";

    /// <summary>The version-mismatch fix's words before its <see cref="StartFix"/> chip.</summary>
    internal const string VersionMismatchFixLead = "quit both and run";

    /// <summary>After three failed refreshes while the process still answers ping (§4.1 Stale, DB §5).</summary>
    internal const string NotResponding = "Sindri not responding: run sindri doctor";

    /// <summary>The not-responding banner's words before its <see cref="DoctorFix"/> chip.</summary>
    internal const string NotRespondingLead = "Sindri not responding: run";

    // The health line's labels (§4.8): Home's strip cells and the Health page's rows.

    /// <summary>The strip's process cell.</summary>
    internal const string CellProcess = "Process";

    /// <summary>The strip's epoch and lease cell.</summary>
    internal const string CellEpoch = "Epoch · lease";

    /// <summary>The API's cell and row.</summary>
    internal const string CellApi = "API";

    /// <summary>The strip's services cell.</summary>
    internal const string CellServices = "Services";

    /// <summary>The strip's profile cell.</summary>
    internal const string CellProfile = "Profile";

    /// <summary>The control plane holds its lease.</summary>
    internal const string LeaseHeld = "held";

    /// <summary>The Health page's pid and epoch row.</summary>
    internal const string RowPidEpoch = "pid · epoch";

    /// <summary>The Health page's lease row.</summary>
    internal const string RowLease = "Host lease";

    /// <summary>The Health page's approved-hash row.</summary>
    internal const string RowApproved = "approved";

    /// <summary>The Health page's live-hash row.</summary>
    internal const string RowLive = "live";

    /// <summary>The Health page's profile file row.</summary>
    internal const string RowFile = "file";

    /// <summary>The G-chord's first key (§8), as Home's strip hints it.</summary>
    internal const string GoKey = "G";

    /// <summary>The G-chord's key for Health (§8: G .).</summary>
    internal const string HealthKey = ".";

    /// <summary>The Refresh button beside a stale pill (§4.1 Stale).</summary>
    internal const string Refresh = "Refresh";

    /// <summary>
    /// A command in flight for 300 ms (§4.1 "sending", §12.4): announced through a polite live region, not shown; the
    /// control shows a spinner in its icon's place instead.
    /// </summary>
    internal const string Sending = "Sending…";

    /// <summary>The freshness word after 30 s with no event or ping.</summary>
    internal const string Stale = "stale";

    /// <summary>The health pill's word for SND-UI-003, an answer this build can't read, or Sindri's 403 after the token retry (§4.1 Unreadable).</summary>
    internal const string Unreadable = "Unreadable";

    /// <summary>The control plane answers but holds no lease.</summary>
    internal const string NoLease = "the control plane answers but holds no lease";

    /// <summary>Not running: no control.json. The card shows the record's path on its own line after these words (I7).</summary>
    internal const string NoRecord = "Sindri hasn't started, or it stopped: no endpoint record at";

    /// <summary>Not running: control.json doesn't parse, or its url isn't absolute http(s); its path follows on its own line.</summary>
    internal const string RecordUnreadable = "The endpoint record can't be read";

    /// <summary>Not running: the platform couldn't read control.json (a directory, no permission).</summary>
    /// <param name="why">Why, in the app's words (never the platform's message, which names the path).</param>
    /// <returns>The reason; the record's path follows on its own line.</returns>
    internal static string RecordUnreadableBecause(string why) => $"{RecordUnreadable}: {why}";

    /// <summary>
    /// Not running (SND-UI-001): control.json names another process or epoch than the one answering. The record and the
    /// answer follow on their own line (<see cref="StaleRecordDetail"/>).
    /// </summary>
    internal const string StaleRecord = "The endpoint record is stale";

    /// <summary>
    /// Not running (SND-UI-002, kill -9 or a crash): nothing answered at the recorded endpoint, or not within 120 s. The
    /// address follows on its own line; the platform's exception text is never shown.
    /// </summary>
    internal const string NothingAnswered = "Nothing answered at the recorded endpoint";

    /// <summary>The stale record's detail line: the record's pid and epoch, then the answer's, naming its pid only when it differs.</summary>
    /// <param name="pid">The record's pid.</param>
    /// <param name="epoch">The record's epoch.</param>
    /// <param name="answeringPid">The answering pid.</param>
    /// <param name="answeringEpoch">The answering epoch.</param>
    /// <returns>"recorded pid 69190 · epoch 4, answering epoch 0".</returns>
    internal static string StaleRecordDetail(int pid, long epoch, int answeringPid, long answeringEpoch) => pid == answeringPid
        ? string.Create(CultureInfo.InvariantCulture, $"recorded pid {pid} · epoch {epoch}, answering epoch {answeringEpoch}")
        : string.Create(CultureInfo.InvariantCulture, $"recorded pid {pid} · epoch {epoch}, answering pid {answeringPid} · epoch {answeringEpoch}");

    /// <summary>The version-mismatch panel's label for this app's version (states.html).</summary>
    internal const string ThisApp = "This app";

    /// <summary>An API version as the version-mismatch panel shows it.</summary>
    /// <param name="version">The version.</param>
    /// <returns>"API v1".</returns>
    internal static string ApiVersion(int version) => string.Create(CultureInfo.InvariantCulture, $"API v{version}");

    /// <summary>The version-mismatch panel's versions line.</summary>
    /// <param name="sindri">The API version Sindri reports.</param>
    /// <param name="app">The API version this app speaks.</param>
    /// <returns>The line.</returns>
    internal static string Versions(int sindri, int app) => string.Create(CultureInfo.InvariantCulture, $"Sindri speaks API v{sindri}; this app speaks v{app}");

    /// <summary>"updated Ns ago" (seconds under a minute, then minutes, then hours).</summary>
    /// <param name="age">How long ago the view was last read.</param>
    /// <returns>The words.</returns>
    internal static string UpdatedAgo(TimeSpan age) => age.TotalSeconds switch
    {
        < 60 => string.Create(CultureInfo.InvariantCulture, $"updated {Math.Max(0, (int)age.TotalSeconds)}s ago"),
        < 3600 => string.Create(CultureInfo.InvariantCulture, $"updated {(int)age.TotalMinutes}m ago"),
        _ => string.Create(CultureInfo.InvariantCulture, $"updated {(int)age.TotalHours}h ago"),
    };
}
