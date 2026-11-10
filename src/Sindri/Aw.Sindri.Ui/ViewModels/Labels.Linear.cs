using System.Globalization;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Work page's Linear view, its switch and the since-you-looked strip (Linear hub spec §3).</summary>
internal static partial class Labels
{
    /// <summary>The switch's first tab: the queue.</summary>
    internal const string WorkViewSindri = "Sindri";

    /// <summary>The switch's second tab, without a count.</summary>
    internal const string WorkViewLinear = "Linear";

    /// <summary>The switch's accessible name.</summary>
    internal const string WorkViewSwitch = "Work view";

    /// <summary>The header button on the Linear view; the New issue form is not built yet.</summary>
    internal const string LinearNewIssue = "New issue";

    /// <summary>The Linear tab's text.</summary>
    /// <param name="news">How many changes the strip holds.</param>
    /// <returns>"Linear" or "Linear · 3 new".</returns>
    internal static string LinearTab(int news) => news > 0 ? string.Create(CultureInfo.InvariantCulture, $"Linear · {news} new") : WorkViewLinear;

    /// <summary>The Linear tab's accessible name.</summary>
    /// <param name="news">How many changes the strip holds.</param>
    /// <returns>"Linear" or "Linear, 3 new".</returns>
    internal static string LinearTabName(int news) => news > 0 ? string.Create(CultureInfo.InvariantCulture, $"Linear, {news} new") : WorkViewLinear;

    /// <summary>The first group's heading.</summary>
    internal const string LinearAssigned = "Assigned to you";

    /// <summary>The second group's heading.</summary>
    internal const string LinearTeams = "Your teams";

    /// <summary>A group's heading with its size.</summary>
    /// <param name="total">How many issues match, not only the loaded ones; null while a next page exists and Sindri has no count.</param>
    /// <param name="shown">How many rows are loaded, said with a plus when the total is unknown.</param>
    /// <returns>"52" with a thousands separator, or "50+".</returns>
    internal static string LinearGroupCount(int? total, int shown) =>
        total is { } all ? all.ToString("N0", CultureInfo.InvariantCulture) : shown.ToString("N0", CultureInfo.InvariantCulture) + "+";

    /// <summary>The button that loads the next 50.</summary>
    internal const string LinearShowMore = "Show more";

    /// <summary>The list's accessible name.</summary>
    internal const string LinearListName = "Linear issues";

    /// <summary>The filter bar's accessible name.</summary>
    internal const string LinearFilterBar = "Filter issues";

    /// <summary>The button that empties every filter.</summary>
    internal const string LinearClearFilters = "Clear filters";

    /// <summary>The box when the filters match nothing.</summary>
    internal const string LinearNoMatch = "No issues match these filters";

    /// <summary>A group's line when nothing is assigned and no filter is on.</summary>
    internal const string LinearNoneAssigned = "No open issues assigned to you";

    /// <summary>A group's line while the viewer step has not run.</summary>
    internal const string LinearFinding = "Finding your issues…";

    /// <summary>The card's title while no Linear source is set up.</summary>
    internal const string LinearConnectTitle = "Linear isn't connected";

    /// <summary>The card's sentence.</summary>
    internal const string LinearConnectBody = "Paste a Linear key and tick your teams under Connections, Linear.";

    /// <summary>The card's button.</summary>
    internal const string LinearConnect = "Connect Linear";

    /// <summary>The line while the feed re-reads every issue.</summary>
    /// <param name="reread">How far it has got; null when the answer does not say.</param>
    /// <returns>"Updating issues… 412 of 1,000", or "Updating issues… 412 so far" when the total is unknown.</returns>
    internal static string LinearUpdating(LinearReread? reread) =>
        reread switch
        {
            { Total: { } total } r => string.Create(CultureInfo.InvariantCulture, $"Updating issues… {r.Done:N0} of {total:N0}"),
            { } r => string.Create(CultureInfo.InvariantCulture, $"Updating issues… {r.Done:N0} so far"),
            null => "Updating issues…",
        };

    /// <summary>The banner while the feed fails; the rows stay.</summary>
    /// <param name="lastSynced">When it last worked, as HH:mm; empty when it never did.</param>
    /// <param name="code">The error code; empty when none was served.</param>
    /// <returns>"Linear sync failing since 22:05 (SND-LINEAR-010)".</returns>
    internal static string LinearFeedFailing(string lastSynced, string code)
    {
        var since = lastSynced.Length > 0 ? " since " + lastSynced : string.Empty;
        return code.Length > 0 ? $"Linear sync failing{since} ({code})" : $"Linear sync failing{since}";
    }

    /// <summary>The line when the API cannot be reached.</summary>
    internal const string LinearSindriDown = "Sindri isn't answering";

    /// <summary>The line when the running Sindri answers but is an older build than the app, so it has no Linear routes (#2247).</summary>
    internal const string LinearSindriOlder = "The running Sindri is older than this app and has no Linear yet. Restart it to use Linear: sindri stop && sindri";

    /// <summary>When the rows shown were read.</summary>
    /// <param name="time">HH:mm.</param>
    /// <returns>"as of 10:05".</returns>
    internal static string LinearAsOf(string time) => "as of " + time;

    /// <summary>A filter's label and its first choice.</summary>
    /// <param name="key">team, project, cycle, state, assignee, priority or label.</param>
    /// <returns>The label.</returns>
    internal static string LinearFilterLabel(string key) => key switch
    {
        "team" => "Team",
        "project" => "Project",
        "cycle" => "Cycle",
        "state" => "State",
        "assignee" => "Assignee",
        "priority" => "Priority",
        "label" => "Label",
        _ => key,
    };

    /// <summary>A filter's choice that leaves it off.</summary>
    /// <param name="key">The filter's key.</param>
    /// <returns>"Open" for the state filter, else "Any team" and the like.</returns>
    internal static string LinearFilterAny(string key) => key == "state" ? "Open" : "Any " + LinearFilterLabel(key).ToLowerInvariant();

    /// <summary>The words for a priority, as a chip carries them (never colour alone).</summary>
    /// <param name="priority">0 to 4.</param>
    /// <returns>"Urgent", "High", "Medium", "Low" or "No priority".</returns>
    internal static string LinearPriority(int priority) => priority switch
    {
        1 => "Urgent",
        2 => "High",
        3 => "Medium",
        4 => "Low",
        _ => "No priority",
    };

    /// <summary>The unassigned row's assignee.</summary>
    internal const string LinearUnassigned = "Unassigned";

    /// <summary>The PR chip on a row.</summary>
    /// <param name="number">The PR's number.</param>
    /// <param name="review">The review state in the Reviews badge's words.</param>
    /// <returns>"PR #2071 waiting on you"; "PR #2071 (state unknown)" when Sindri couldn't read the pull request.</returns>
    internal static string LinearPrChip(int number, string review) =>
        string.Create(CultureInfo.InvariantCulture, $"PR #{number} ") + review switch
        {
            "waiting" => "waiting on you",
            "unknown" => "(state unknown)",
            _ => review,
        };

    /// <summary>A row's accessible name.</summary>
    /// <param name="row">The issue.</param>
    /// <returns>"ENG-12, title, In Progress, Joi, Urgent, running"; the last part is left out when no item works the issue.</returns>
    internal static string LinearRowName(LinearIssueRow row) =>
        $"{row.Key}, {row.Title}, {row.State.Name}, {row.Assignee?.Name ?? LinearUnassigned}, {LinearPriority(row.Priority)}" + (row.Link is { } link ? ", " + link.ItemState : string.Empty);

    /// <summary>The strip's region name.</summary>
    /// <param name="count">How many changes.</param>
    /// <returns>"Since you last looked, 3 items".</returns>
    internal static string LinearStripName(int count) => string.Create(CultureInfo.InvariantCulture, $"Since you last looked, {count} {(count == 1 ? "item" : "items")}");

    /// <summary>One change in the strip.</summary>
    /// <param name="item">The change.</param>
    /// <returns>"ENG-12 assigned to you", "Sam Lee mentioned you on ENG-12" or "ENG-12 moved from Todo to In Progress"; a row whose actor is known names them ("ENG-12 assigned to you by Sam Lee").</returns>
    internal static string LinearSinceRow(LinearSinceItem item)
    {
        var by = item.Actor is { } actor ? " by " + actor.Name : string.Empty;
        return item.Kind switch
        {
            "assigned" => $"{item.Key} assigned to you{by}",
            "mentioned" when item.Actor is { } author => $"{author.Name} mentioned you on {item.Key}",
            "mentioned" => $"You were mentioned on {item.Key}",
            "state" => $"{item.Key} {StateMove(item)}{by}",
            _ => $"{item.Key} changed{by}",
        };
    }

    // why: a name the server could not find is null; the row says what it knows and never leaves a blank (#2536)
    private static string StateMove(LinearSinceItem item) => (item.From, item.To) switch
    {
        ({ Length: > 0 } from, { Length: > 0 } to) => $"moved from {from} to {to}",
        (_, { Length: > 0 } to) => $"moved to {to}",
        _ => "moved to a new state",
    };

    /// <summary>One change of an issue, without its key.</summary>
    /// <param name="item">The change.</param>
    /// <returns>"assigned to you", "moved from Todo to In Progress", "Sam Lee mentioned you", "you were mentioned" or "changed".</returns>
    internal static string LinearSinceChange(LinearSinceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var by = item.Actor is { } actor ? " by " + actor.Name : string.Empty;
        return item.Kind switch
        {
            "assigned" => "assigned to you" + by,
            "mentioned" when item.Actor is { } author => author.Name + " mentioned you",
            "mentioned" => "you were mentioned",
            "state" => StateMove(item) + by,
            _ => "changed" + by,
        };
    }

    /// <summary>The state chip of a move.</summary>
    /// <param name="from">The old state's name; null when unknown.</param>
    /// <param name="to">The new state's name; null when unknown.</param>
    /// <returns>"Todo → In Progress", "→ In Progress" or "New state".</returns>
    internal static string LinearStateChip(string? from, string? to) => (from, to) switch
    {
        ({ Length: > 0 } a, { Length: > 0 } b) => a + " → " + b,
        (_, { Length: > 0 } b) => "→ " + b,
        _ => "New state",
    };

    /// <summary>A since-you-looked row's second line.</summary>
    /// <param name="key">The issue key.</param>
    /// <param name="changes">What the chips don't show, newest first; may be empty.</param>
    /// <returns>"ENG-12" or "ENG-12 · Sam Lee mentioned you".</returns>
    internal static string LinearSinceDetail(string key, IEnumerable<string> changes) => string.Join(" · ", new[] { key }.Concat(changes));

    /// <summary>A group's button that shows its other rows.</summary>
    /// <param name="count">How many rows the group holds.</param>
    /// <returns>"Show all 7".</returns>
    internal static string LinearShowAll(int count) => string.Create(CultureInfo.InvariantCulture, $"Show all {count}");

    /// <summary>The same button once the group is expanded.</summary>
    internal const string LinearShowFewer = "Show fewer";

    /// <summary>A person's initials for a row.</summary>
    /// <param name="name">The display name.</param>
    /// <returns>"JE" for "Joi Example", "J" for "Joi"; empty for a blank name.</returns>
    internal static string LinearInitials(string name)
    {
        var words = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0 ? string.Empty : string.Concat(words.Take(2).Select(static w => char.ToUpperInvariant(w[0])));
    }

    /// <summary>The reason a Linear feed failed, in words.</summary>
    /// <param name="code">The feed's error code; may be empty.</param>
    /// <returns>The sentence the card shows.</returns>
    internal static string LinearFeedReason(string? code) => code switch
    {
        "SND-CONTEXT-071" => "Linear refused the key. It may have expired or been revoked.",
        "SND-CONTEXT-010" => "Linear is rate-limiting Sindri or is down. Sindri keeps trying.",
        "SND-CONTEXT-072" => "Linear sent a reply Sindri couldn't read.",
        _ => "Linear sync is failing.",
    };

    /// <summary>The Retry button of the card's smaller states.</summary>
    internal const string LinearStripRetry = "Retry";

    /// <summary>New issue's tooltip while Linear is not connected.</summary>
    internal const string LinearNewIssueNeedsConnection = "Connect Linear first: New issue needs a Linear connection.";

    /// <summary>The card's one action: moves the marker.</summary>
    internal const string LinearMarkAllSeen = "Mark all seen";

    /// <summary>The card's line when the changes cannot be read.</summary>
    internal const string LinearStripFailed = "Couldn't load what changed";

    /// <summary>The card's one button when the changes cannot be read: opens the Linear connection.</summary>
    internal const string LinearReconnect = "Reconnect";

    /// <summary>The card's line when nothing is new.</summary>
    internal const string LinearNothingNew = "Nothing new since you last looked";

    /// <summary>The card's second line when nothing is new and the marker's time is known.</summary>
    /// <param name="age">How long ago the newest change you saw happened.</param>
    /// <returns>"The last change you saw was 2h ago".</returns>
    internal static string LinearNothingNewWhen(TimeSpan age) => "The last change you saw was " + AgoWords(age);

    /// <summary>The group of issues assigned to the viewer.</summary>
    internal const string LinearSinceAssigned = "Assigned to you";

    /// <summary>The group of issues that changed state.</summary>
    internal const string LinearSinceMoved = "Moved";

    /// <summary>The group of everything else (mentions).</summary>
    internal const string LinearSinceOther = "Other";
}
