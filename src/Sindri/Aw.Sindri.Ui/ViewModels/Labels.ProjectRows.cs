using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Projects page's rows, views, sort and groups (issue 3717, frames round 11). The health reason is served and never here.</summary>
internal static partial class Labels
{
    /// <summary>The Needs me view's tab.</summary>
    internal const string ProjectsViewNeedsMe = "Needs me";

    /// <summary>The My work view's tab.</summary>
    internal const string ProjectsViewMyWork = "My work";

    /// <summary>The Projects view's tab: every open project.</summary>
    internal const string ProjectsViewAll = "Projects";

    /// <summary>The Triage view's tab.</summary>
    internal const string ProjectsViewTriage = "Triage";

    /// <summary>A view's tab as a screen reader says it: "Needs me, 6 projects".</summary>
    internal static string ProjectsViewName(string view, int count) => string.Create(CultureInfo.InvariantCulture, $"{view}, {count} {(count == 1 ? "project" : "projects")}");

    /// <summary>The Sort button's label word.</summary>
    internal const string ProjectsSortLabel = "Sort";

    /// <summary>The Sort menu's heading.</summary>
    internal const string ProjectsSortBy = "Sort by";

    /// <summary>The Group button's label word.</summary>
    internal const string ProjectsGroupLabel = "Group";

    /// <summary>The Group menu's heading.</summary>
    internal const string ProjectsGroupBy = "Group by";

    /// <summary>The Last activity sort.</summary>
    internal const string ProjectsSortActivity = "Last activity";

    /// <summary>The Last activity sort's hint in the menu.</summary>
    internal const string ProjectsSortActivityHint = "newest first";

    /// <summary>The Target date sort.</summary>
    internal const string ProjectsSortTarget = "Target date";

    /// <summary>The Target date sort's hint.</summary>
    internal const string ProjectsSortTargetHint = "soonest first";

    /// <summary>The Name sort.</summary>
    internal const string ProjectsSortName = "Name";

    /// <summary>The Name sort's hint.</summary>
    internal const string ProjectsSortNameHint = "A to Z";

    /// <summary>Grouping by health.</summary>
    internal const string ProjectsGroupHealth = "Health";

    /// <summary>Grouping by health's hint in the menu.</summary>
    internal const string ProjectsGroupHealthHint = "off track, at risk…";

    /// <summary>Grouping by state.</summary>
    internal const string ProjectsGroupState = "State";

    /// <summary>Grouping by state's hint.</summary>
    internal const string ProjectsGroupStateHint = "in progress, planned…";

    /// <summary>Grouping by repo.</summary>
    internal const string ProjectsGroupRepo = "Repo";

    /// <summary>Grouping by repo's hint.</summary>
    internal const string ProjectsGroupRepoHint = "one group per repo";

    /// <summary>No grouping: the button and the menu say the same words.</summary>
    internal const string ProjectsGroupNone = "No grouping";

    /// <summary>The Sort or Group button's accessible name: "Sort: Last activity".</summary>
    internal static string ProjectsMenuButtonName(string label, string word) => label + ": " + word;

    /// <summary>A health group's head for Sindri's level; a level this build doesn't know is shown as served, first letter up.</summary>
    internal static string ProjectHealthGroup(string? level) => level switch
    {
        null or "" => ProjectsNoOpenPrs,
        "offTrack" => "Off track",
        "atRisk" => "At risk",
        "unknown" => "CI not checked",
        "onTrack" => "On track",
        _ => char.ToUpperInvariant(level[0]) + level[1..],
    };

    /// <summary>The group of projects with no open PR that is not a draft (health null), by health or by repo.</summary>
    internal const string ProjectsNoOpenPrs = "No open PRs";

    /// <summary>The failed-read line's tail while the owner's grouping is Health or Repo: the rows show state groups instead.</summary>
    internal const string ProjectsGroupedByStateUntilThen = " · grouped by state until then";

    /// <summary>The first health group's hint, on the right of its head.</summary>
    internal const string ProjectsHealthHint = "derived from each project’s open PRs";

    /// <summary>The column heads, left to right.</summary>
    internal const string ProjectsColumnProject = "Project";

    /// <summary>The PRs column's head.</summary>
    internal const string ProjectsColumnPrs = "PRs";

    /// <summary>The Items column's head.</summary>
    internal const string ProjectsColumnItems = "Items";

    /// <summary>The Agents column's head.</summary>
    internal const string ProjectsColumnAgents = "Agents";

    /// <summary>The Blocked on column's head.</summary>
    internal const string ProjectsColumnBlockedOn = "Blocked on";

    /// <summary>The Target column's head.</summary>
    internal const string ProjectsColumnTarget = "Target";

    /// <summary>The Active column's head.</summary>
    internal const string ProjectsColumnActive = "Active";

    /// <summary>The Blocked on cell when the server says a PR waits on the owner.</summary>
    internal const string ProjectBlockedOnYou = "you";

    /// <summary>The Blocked on cell in Needs me when a PR waits on the owner's review: the ask after "you".</summary>
    internal const string ProjectBlockedOnYouReview = "you · review";

    /// <summary>The search field's placeholder.</summary>
    internal const string ProjectsSearchPlaceholder = "Search name, note, lead";

    /// <summary>The search field's accessible name.</summary>
    internal const string ProjectsSearchName = "Search projects";

    /// <summary>The search field's count: "2 of 32" (the rows of the view shown).</summary>
    internal static string ProjectsSearchCount(int shown, int of) => string.Create(CultureInfo.InvariantCulture, $"{shown} of {of}");

    /// <summary>A row that matched the search only in its note (the note itself is not printed on a row).</summary>
    internal const string ProjectMatchedInNote = "matched in note";

    /// <summary>The search with no match: its title.</summary>
    internal static string ProjectsSearchNone(string query) => "No project matches “" + query + "”";

    /// <summary>The search with no match: what search looks at.</summary>
    internal const string ProjectsSearchNoneBody = "Search looks at the name, the note and the lead.";

    /// <summary>The key that clears the search, on its button.</summary>
    internal const string ProjectsClearSearchKey = "Esc";

    /// <summary>The search with no match: its button.</summary>
    internal const string ProjectsClearSearch = "Clear search";

    /// <summary>The footer's key hints while the search field has focus.</summary>
    internal static readonly (string Key, string Word)[] ProjectsSearchKeyHints =
    [
        ("Esc", "clear"),
        ("↓", "to the list"),
        ("Enter", "open"),
    ];

    /// <summary>A group head's toggle: "Off track, 3, collapsed".</summary>
    internal static string ProjectsGroupToggleName(string heading, int count, bool collapsed) =>
        string.Create(CultureInfo.InvariantCulture, $"{heading}, {count}, {(collapsed ? "collapsed" : "expanded")}");

    /// <summary>A target before today: "Nov 12 passed".</summary>
    internal static string ProjectTargetPassed(string day) => day + " passed";

    /// <summary>The Items cell: "5/12" (done of all, cancelled ones left out).</summary>
    internal static string ProjectItems(int done, int total) => string.Create(CultureInfo.InvariantCulture, $"{done}/{total}");

    /// <summary>A row's age: "&lt;1m", minutes, hours, days, then weeks.</summary>
    internal static string ProjectRowAge(TimeSpan age) => age.TotalDays switch
    {
        < 7 => RowAge(age),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)(age.TotalDays / 7)}w"),
    };

    /// <summary>One PR glyph's words for a screen reader: "2 CI red".</summary>
    internal static string ProjectPrGlyphName(string state, int count) => string.Create(CultureInfo.InvariantCulture, $"{count} ") + state switch
    {
        "ciRed" => "CI red",
        "waiting" => "waiting on review",
        "unknown" => "CI not checked",
        "ready" => "ready",
        "draft" => count == 1 ? "draft" : "drafts",
        _ => state,
    };

    /// <summary>The page width from which the Agents column shows (issue 3717 qa: 868 and up keeps every column).</summary>
    internal const double ProjectsAgentsFrom = 868;

    /// <summary>The page width from which Items, Target and the health reason show (808 to 867 drops Agents only; under 808 the 1000-wide set: name, PRs, Blocked on, Active).</summary>
    internal const double ProjectsWideFrom = 808;

    /// <summary>The Needs me view with no project.</summary>
    internal const string ProjectsNeedsMeEmptyTitle = "Nothing waits on you";

    /// <summary>The Needs me view's empty body.</summary>
    internal const string ProjectsNeedsMeEmptyBody = "No PR waits on your review, and no project you lead has red CI.";

    /// <summary>The My work view with no project.</summary>
    internal const string ProjectsMyWorkEmptyTitle = "No projects of yours yet";

    /// <summary>The My work view's empty body (decisions comment: attached projects are not in My work).</summary>
    internal const string ProjectsMyWorkEmptyBody = "Projects you lead or make by hand show here, grouped by state.";

    /// <summary>The Triage view, not built yet.</summary>
    internal const string ProjectsTriageTitle = "Triage isn’t built yet";

    /// <summary>The Triage view's body.</summary>
    internal const string ProjectsTriageBody = "When it is, it will hold new Linear projects and project updates that wait for a decision: accept, decline, mark as duplicate or snooze. It needs Linear connected.";

    /// <summary>The empty views' button to every project.</summary>
    internal const string ProjectsOpenAll = "Open Projects";

    /// <summary>The Triage view's button back to Needs me.</summary>
    internal const string ProjectsBackToNeedsMe = "Back to Needs me";

    /// <summary>The footer's key hints, key then word.</summary>
    internal static readonly (string Key, string Word)[] ProjectsKeyHints =
    [
        ("j/k", "move"),
        ("Enter", "open"),
        ("/", "search"),
        ("o", "sort"),
        ("b", "group"),
        ("1-4", "view"),
        ("Ctrl+K", "palette"),
    ];

    /// <summary>The footer's key hints in an empty view: no row keys.</summary>
    internal static readonly (string Key, string Word)[] ProjectsEmptyKeyHints =
    [
        ("/", "search"),
        ("1-4", "view"),
        ("Ctrl+K", "palette"),
    ];
}
