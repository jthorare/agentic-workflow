using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the System page's tabs, cards and the scheduled services. Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The first tab: the health headline and its cards.</summary>
    internal const string SystemOverviewTab = "Overview";

    /// <summary>The tabs' accessible name.</summary>
    internal const string SystemTabsName = "System views";

    /// <summary>The heading over the problem lines.</summary>
    internal const string SystemNeedsALook = "Needs a look";

    /// <summary>The Views card's name.</summary>
    internal const string ViewsCard = "Views";

    /// <summary>The Agents card's name.</summary>
    internal const string AgentsCard = "Agents";

    /// <summary>The Views card's state when current.</summary>
    internal const string ViewsUpToDate = "Up to date";

    /// <summary>The Views card's state while rebuilding.</summary>
    internal const string ViewsCardRebuilding = "Rebuilding";

    /// <summary>The Views card's state when stalled.</summary>
    internal const string ViewsStalled = "Stalled";

    /// <summary>The Views card's row for how far the views have read.</summary>
    internal const string ViewsReadUpTo = "Read up to";

    /// <summary>The Views card's row for when they last changed.</summary>
    internal const string ViewsUpdated = "Updated";

    /// <summary>The Agents card's row for running sessions.</summary>
    internal const string AgentsRunning = "Running";

    /// <summary>The Agents card's row for sessions waiting on the person.</summary>
    internal const string AgentsAsking = "Asking you";

    /// <summary>The Agents card's row for sources that failed.</summary>
    internal const string AgentsProblems = "Problems";

    /// <summary>The Views card with nothing to say yet.</summary>
    internal const string ViewsUnknown = "Not read yet";

    /// <summary>The Services table's columns.</summary>
    internal const string ServiceColumnName = "Service";

    /// <summary>The Services table's cadence column.</summary>
    internal const string ServiceColumnEvery = "Every";

    /// <summary>The Services table's status column.</summary>
    internal const string ServiceColumnStatus = "Status";

    /// <summary>The Services table's description column.</summary>
    internal const string ServiceColumnWhat = "What it does";

    /// <summary>The Services table's last-run column.</summary>
    internal const string ServiceColumnLastRun = "Last run";

    /// <summary>A service that runs on its schedule.</summary>
    internal const string ServiceScheduled = "Scheduled";

    /// <summary>A service whose scheduled slot passed without the scheduler running it.</summary>
    internal const string ServiceOverdue = "Overdue";

    /// <summary>"missed its 12:01 run": the detail under an overdue service.</summary>
    internal static string ServiceMissed(string time) => "missed its " + time + " run";

    /// <summary>The overview's name for overdue services: "heartbeat overdue", "heartbeat, setup overdue".</summary>
    internal static string ServicesOverdue(IEnumerable<string> names) => string.Join(", ", names) + " overdue";

    /// <summary>A service whose manual run is in flight.</summary>
    internal const string ServiceRunning = "Running now";

    /// <summary>A paused service.</summary>
    internal const string ServicePaused = "Paused";

    /// <summary>A service whose latest run failed.</summary>
    internal const string ServiceLastRunFailed = "Last run failed";

    /// <summary>A service that has not run.</summary>
    internal const string ServiceNotRun = "not run yet";

    /// <summary>The Actions card's name for what the app does itself.</summary>
    internal const string ActionsReady = "in this app";

    /// <summary>The count of actions the app shows as a command to copy.</summary>
    internal const string ActionsCopy = "to copy";

    /// <summary>The page width under which the Services table keeps three columns (system spec §2).</summary>
    internal const double SystemWideFrom = 900;

    /// <summary>The link on a row whose manual run is in flight.</summary>
    internal const string ViewJob = "View job";

    /// <summary>The Services tab: "Services (6)".</summary>
    /// <param name="count">How many services are scheduled.</param>
    /// <returns>The tab's label.</returns>
    internal static string ServicesTab(int count) => string.Create(CultureInfo.InvariantCulture, $"{Services} ({count})");

    /// <summary>The View job link's accessible name.</summary>
    /// <param name="service">The service's name.</param>
    /// <returns>"View job for heartbeat".</returns>
    internal static string ViewJobName(string service) => "View job for " + service;

    /// <summary>The Services card's link: "6 scheduled".</summary>
    /// <param name="count">How many services are scheduled.</param>
    /// <returns>The link's label.</returns>
    internal static string ServicesScheduled(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} scheduled");

    /// <summary>A service's cadence: "every 5s".</summary>
    /// <param name="every">The cadence as the profile writes it.</param>
    /// <returns>The words.</returns>
    internal static string ServiceEvery(string every) => "every " + every;

    /// <summary>The Views card's progress: "event 214 of 214".</summary>
    /// <param name="cursor">The event the views have read up to.</param>
    /// <param name="head">The newest event.</param>
    /// <returns>The words.</returns>
    internal static string ViewsProgress(long cursor, long head) => string.Create(CultureInfo.InvariantCulture, $"event {cursor} of {head}");

    /// <summary>The Actions card's counts: "49 in this app, 14 to copy".</summary>
    /// <param name="ready">Actions the app runs.</param>
    /// <param name="copy">Actions the app shows as a command to copy.</param>
    /// <returns>The words.</returns>
    internal static string ActionCounts(int ready, int copy) =>
        string.Create(CultureInfo.InvariantCulture, $"{ready} {ActionsReady}, {copy} {ActionsCopy}");

    /// <summary>What a built-in service does, in plain words; empty for a service the app does not know.</summary>
    /// <param name="name">The service's name.</param>
    /// <returns>One short sentence, or empty.</returns>
    internal static string ServiceWhat(string name) => name switch
    {
        "heartbeat" => "Records that Sindri is alive.",
        "context-concept-scan" => "Has a model look for product concepts in each new map: a cheap one for a small map, a stronger one for a large map.",
        "context-map-refresh" => "Builds the context map again so it stays current.",
        "context-sync-gaps" => "Starts a sync for a source that has never synced.",
        "pr-watch" => "Watches the pull requests that wait on the forge.",
        "router-stub" => "Routes each started item to its workflow.",
        "setup" => "Keeps the setup checklist in step with the profile.",
        "workflow-events" => "Passes ledger events to the running workflows.",
        _ => string.Empty,
    };
}
