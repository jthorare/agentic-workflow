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
    /// <param name="total">How many issues match, not only the loaded ones.</param>
    /// <returns>"52" with a thousands separator.</returns>
    internal static string LinearGroupCount(int total) => total.ToString("N0", CultureInfo.InvariantCulture);

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
    internal const string LinearConnectBody = "Paste a Linear key and tick your teams in Settings, Connections, Linear.";

    /// <summary>The card's button.</summary>
    internal const string LinearConnect = "Connect Linear";

    /// <summary>The line while the feed re-reads every issue.</summary>
    /// <param name="reread">How far it has got; null when the answer does not say.</param>
    /// <returns>"Updating issues… 412 of 1,000".</returns>
    internal static string LinearUpdating(LinearReread? reread) =>
        reread is { } r ? string.Create(CultureInfo.InvariantCulture, $"Updating issues… {r.Done:N0} of {r.Total:N0}") : "Updating issues…";

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
    /// <returns>"PR #2071 waiting on you".</returns>
    internal static string LinearPrChip(int number, string review) =>
        string.Create(CultureInfo.InvariantCulture, $"PR #{number} ") + (string.Equals(review, "waiting", StringComparison.Ordinal) ? "waiting on you" : review);

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
            "state" => $"{item.Key} moved from {item.From} to {item.To}{by}",
            _ => $"{item.Key} changed{by}",
        };
    }

    /// <summary>The strip's button that moves the marker.</summary>
    internal const string LinearDismiss = "Dismiss";

    /// <summary>The strip's line when the changes cannot be read.</summary>
    internal const string LinearStripFailed = "Couldn't load what changed";

    /// <summary>The button that reads them again.</summary>
    internal const string LinearStripRetry = "Retry";
}
