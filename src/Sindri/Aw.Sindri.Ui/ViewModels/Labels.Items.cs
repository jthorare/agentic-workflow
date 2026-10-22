using System.Collections.Frozen;
using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the queue, Home's queue panels and the item sheet (§4.2, §4.5, §4.6, §12.4).</summary>
internal static partial class Labels
{
    // An ItemState value as a word (§4.5 "state as a word"); a value not here shows itself.
    private static readonly FrozenDictionary<string, string> _states = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["new"] = "New",
        ["queued"] = "Queued",
        ["running"] = "Running",
        ["needs-decision"] = "Needs decision",
        ["needs-answer"] = "Needs answer",
        ["parked"] = "Paused: needs a decision",
        ["taken-over"] = "With you",
        ["done"] = "Done",
        ["cancelled"] = "Cancelled",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // A queue group's heading (API §3.6's names); a group not here shows its name.
    private static readonly FrozenDictionary<string, string> _groups = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["waitingOnYou"] = "Waiting on you",
        ["withYou"] = "With you",
        ["parked"] = "Paused",
        ["running"] = "Running",
        ["queued"] = "Queued",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The item sheet's Details line for the item's id.</summary>
    internal const string ItemIdLabel = "Item id";

    /// <summary>The Copy button next to the item's id.</summary>
    internal const string CopyItemId = "Copy";

    /// <summary>The Queue table's column headings (queue.html); the title leads, the raw id is in the sheet's Details.</summary>

    /// <summary>The title column.</summary>
    internal const string ColumnTitle = "Title";

    /// <summary>The repo column.</summary>
    internal const string ColumnRepo = "Repo";

    /// <summary>The state column.</summary>
    internal const string ColumnState = "Status";

    /// <summary>The step column.</summary>
    internal const string ColumnStep = "Stage";

    /// <summary>The since column.</summary>
    internal const string ColumnSince = "Age";

    /// <summary>The stage of an item that has no step yet.</summary>
    internal const string NotStarted = "Not started";

    /// <summary>An item's step in plain words (F3 spec work §4): the line's five stages; any other step shows as served.</summary>
    /// <param name="step">The served step, or null.</param>
    /// <returns>The plain name and whether it is the served word.</returns>
    internal static (string Name, bool Raw) StageName(string? step) => step switch
    {
        null or "" => (NotStarted, false),
        "scoping" => ("Scoping", false),
        "design" => ("Design", false),
        "plan" => ("Plan", false),
        "phase" => (WorkflowsLaneBuild, false),
        "merge" => ("Merge", false),
        _ => (step, true),
    };

    /// <summary>The top bar's hint while the G chord waits (§8).</summary>
    internal const string ChordHint = "G …";

    /// <summary>The badge on a parked item in Waiting on you (§4.5).</summary>
    internal const string ParkedBadge = "paused";

    /// <summary>The Load more button under a timeline with a next page.</summary>
    internal const string LoadMore = "Load more";

    /// <summary>The Load more button's accessible name: which timeline it extends.</summary>
    /// <param name="itemId">The item's id.</param>
    /// <returns>"Load more timeline for ITM-…".</returns>
    internal static string LoadMoreFor(string itemId) => LoadMore + " timeline for " + itemId;

    /// <summary>The item sheet's timeline heading.</summary>
    internal const string Timeline = "History";

    /// <summary>The item sheet's actions heading.</summary>
    internal const string ItemActions = "ACTIONS";

    /// <summary>The item header's tracker link label.</summary>
    internal const string Link = "Link";

    /// <summary>The "created by" label in the item header.</summary>
    internal const string CreatedBy = "Created by";

    /// <summary>The "waiting on" label in the item header.</summary>
    internal const string WaitingOn = "Waiting for";

    /// <summary>The "instance" label in the item header.</summary>
    internal const string Instance = "Run";

    /// <summary>A destructive control's Confirm.</summary>
    internal const string Confirm = "Confirm";

    /// <summary>A destructive control's Keep (nothing is sent).</summary>
    internal const string Keep = "Keep";

    /// <summary>The create form's title field.</summary>
    internal const string ItemTitle = "Title";

    /// <summary>The create form's title field: it says the field is required (L10).</summary>
    internal const string TitleRequired = "Title (required)";

    /// <summary>What a blank submit says under the title field and through its live region (L10).</summary>
    internal const string TitleIsRequired = "Title is required";

    /// <summary>The create form's repo field.</summary>
    internal const string ItemRepo = "Repo";

    /// <summary>The create form's body field.</summary>
    internal const string ItemBody = "Body";

    /// <summary>The create form's Cancel (closes it, nothing sent).</summary>
    internal const string Cancel = "Cancel";

    /// <summary>An ItemState value as a word.</summary>
    /// <param name="state">The API's value.</param>
    /// <returns>The word.</returns>
    internal static string ItemState(string state) => _states.TryGetValue(state, out var word) ? word : state;

    /// <summary>A queue group's heading.</summary>
    /// <param name="group">The API's group name.</param>
    /// <returns>The heading.</returns>
    internal static string Group(string group) => _groups.TryGetValue(group, out var label) ? label : group;

    /// <summary>Home's left panel header (§12.4): "RUNNING N · WITH YOU N".</summary>
    /// <param name="running">The running group's count.</param>
    /// <param name="withYou">The with-you group's count.</param>
    /// <returns>The header.</returns>
    internal static string RunningHeader(int running, int withYou) => string.Create(CultureInfo.InvariantCulture, $"RUNNING {running} · WITH YOU {withYou}");

    /// <summary>Home's right panel header (§12.4): "QUEUED N".</summary>
    /// <param name="queued">The queued group's count.</param>
    /// <returns>The header.</returns>
    internal static string QueuedHeader(int queued) => string.Create(CultureInfo.InvariantCulture, $"QUEUED {queued}");

    /// <summary>A capped panel's link (§3.2): "Show all N in Queue".</summary>
    /// <param name="count">How many rows the group holds.</param>
    /// <returns>The words.</returns>
    internal static string ShowAllInQueue(int count) => string.Create(CultureInfo.InvariantCulture, $"Show all {count} in Queue");

    /// <summary>The announcement when the focused row clears on a refresh (§3.3): "ITM-… cleared".</summary>
    /// <param name="id">The row's id.</param>
    /// <returns>The words.</returns>
    internal static string Cleared(string id) => id + " cleared";

    /// <summary>The two together, when "N new" and "cleared" come in one refresh: one sentence, spoken once.</summary>
    /// <param name="newRows">The pill's words.</param>
    /// <param name="id">The cleared row's id.</param>
    /// <returns>The words.</returns>
    internal static string NewAndCleared(string newRows, string id) => $"{newRows}, {Cleared(id)}";

    /// <summary>The pill over a panel while new rows are held (§8): "N new".</summary>
    /// <param name="count">How many rows are held.</param>
    /// <returns>The words.</returns>
    internal static string NewRows(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} new");

    /// <summary>The Queue page's filter field (§8 `/`).</summary>
    internal const string Filter = "Filter by id, title, repo";

    /// <summary>The item header's version, for Details: "2".</summary>
    /// <param name="version">The item's version.</param>
    /// <returns>The words.</returns>
    internal static string Version(long version) => string.Create(CultureInfo.InvariantCulture, $"{version}");

    /// <summary>An age as the rows show it (§4.3 "the age, from since"): "12s", "12m", "2h 56m", "9h", "3d".</summary>
    /// <param name="age">How long since.</param>
    /// <returns>The words.</returns>
    internal static string Age(TimeSpan age) => age.TotalSeconds switch
    {
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, (int)age.TotalSeconds)}s"),
        < 3600 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes}m"),
        < 86400 when age.Minutes == 0 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours}h"),
        < 86400 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours}h {age.Minutes}m"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays}d"),
    };

    /// <summary>The Details row for the item's version.</summary>
    internal const string SheetVersion = "Version";

    /// <summary>The item sheet's disclosure heading: who made it, the version and the run's address.</summary>
    internal const string SheetDetails = "Details";

    /// <summary>The sheet's one status sentence: the state as a word and how long the item has been in it.</summary>
    /// <param name="state">The state as a word ("Running").</param>
    /// <param name="age">The age as the rows show it ("2h 56m").</param>
    /// <returns>"Running for 2h 56m".</returns>
    internal static string StatusFor(string state, string age) => state + " for " + age;
}
