using System.Globalization;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The Projects rows' rules (issue 3717, frames round 11, decisions comment 7073539653), as pure functions of what the server served:
/// which projects a view holds, their order, their groups, and the words of a row's cells. Health and its reason come only from the
/// server's truth; nothing here counts PRs into a health.
/// </summary>
internal static class ProjectRows
{
    // The health groups' order, by the served level; a project with no health (no open PR that is not a draft) follows them.
    private static readonly string[] _healthOrder = ["offTrack", "atRisk", "unknown", "onTrack"];

    private static readonly string[] _stateOrder = ["inProgress", "planned", "paused", "backlog"];

    // The PRs cell's glyphs, left to right; drafts come last, after a hairline, since they never count toward health.
    private static readonly string[] _prOrder = ["ciRed", "waiting", "unknown", "ready"];

    /// <summary>True when Sindri couldn't read its own PR, check and agent facts: the page then has a "sindri" freshness row, and every card's truth is empty.</summary>
    /// <param name="list">The list as served.</param>
    /// <returns>True when the facts are missing.</returns>
    internal static bool FactsMissing(ProjectList list) =>
        list.Freshness.Any(f => Is(f.Source, "sindri") && !Is(f.State, "ok"));

    /// <summary>True when a view holds this open project.</summary>
    /// <param name="view">The view.</param>
    /// <param name="card">The project as served.</param>
    /// <returns>True when the view shows it.</returns>
    internal static bool InView(ProjectView view, ProjectCardView card) => view switch
    {
        ProjectView.NeedsMe => card.Truth?.BlockedOnYou == true,
        ProjectView.MyWork => Is(card.Origin, "led") || Is(card.Origin, "handMade"),
        ProjectView.All => true,
        ProjectView.Triage => false,
        _ => false,
    };

    /// <summary>
    /// The projects in the sort's order. Last activity is newest first, then name; a project with no last activity (an attached one while Context can't
    /// answer) goes after every project that has one, by name. Target date is soonest first, then name; no target (or one that isn't a day) goes last, by name.
    /// </summary>
    /// <param name="cards">The projects as served.</param>
    /// <param name="sort">The sort.</param>
    /// <returns>The projects, sorted.</returns>
    internal static IReadOnlyList<ProjectCardView> Sorted(IEnumerable<ProjectCardView> cards, ProjectSort sort) => sort switch
    {
        ProjectSort.Target => [.. cards.OrderBy(c => TargetDay(c.Target) is null).ThenBy(c => TargetDay(c.Target)).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Id, StringComparer.Ordinal)],
        ProjectSort.Name => [.. cards.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Id, StringComparer.Ordinal)],
        ProjectSort.Activity => ByActivity(cards),
        _ => ByActivity(cards),
    };

    private static List<ProjectCardView> ByActivity(IEnumerable<ProjectCardView> cards) =>
        [.. cards.OrderBy(c => c.LastActivity is null).ThenByDescending(c => c.LastActivity).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Id, StringComparer.Ordinal)];

    /// <summary>
    /// The groups of a sorted list, in their fixed order; groups with no project are left out. Grouping by health or by repo reads the truth, so
    /// while the truth is missing the rows are grouped by state instead (the page says why on one line, and the Group button keeps the owner's pick).
    /// Grouping none is one group with no head.
    /// </summary>
    /// <param name="cards">The projects, sorted.</param>
    /// <param name="grouping">The grouping.</param>
    /// <param name="factsMissing">True when Sindri couldn't read its PR, check and agent facts.</param>
    /// <returns>The groups.</returns>
    internal static IReadOnlyList<ProjectRowGroup> Grouped(IReadOnlyList<ProjectCardView> cards, ProjectGrouping grouping, bool factsMissing)
    {
        if (cards.Count == 0)
        {
            return [];
        }

        return grouping switch
        {
            ProjectGrouping.None => [new ProjectRowGroup(string.Empty, string.Empty, cards)],
            ProjectGrouping.Health when !factsMissing => ByHealth(cards),
            ProjectGrouping.Repo when !factsMissing => ByRepo(cards),
            ProjectGrouping.Health or ProjectGrouping.Repo or ProjectGrouping.State => ByState(cards),
            _ => ByState(cards),
        };
    }

    /// <summary>The PRs cell: one glyph and count per state, in the cell's order, the drafts last.</summary>
    /// <param name="truth">The project's truth; null from an older server.</param>
    /// <returns>The glyphs; none when no PR is open.</returns>
    internal static IReadOnlyList<ProjectPrGlyph> Glyphs(ProjectTruthView? truth)
    {
        if (truth is null)
        {
            return [];
        }

        var glyphs = new List<ProjectPrGlyph>();
        foreach (var state in _prOrder)
        {
            var count = truth.OpenPrs.Count(p => Is(p.State, state));
            if (count > 0)
            {
                glyphs.Add(new ProjectPrGlyph(state, count, afterHairline: false));
            }
        }

        var drafts = truth.OpenPrs.Count(p => Is(p.State, "draft"));
        if (drafts > 0)
        {
            glyphs.Add(new ProjectPrGlyph("draft", drafts, afterHairline: glyphs.Count > 0));
        }

        return glyphs;
    }

    /// <summary>True when the server says the project or one of its PRs waits on the owner (issue 3797).</summary>
    /// <param name="truth">The project's truth.</param>
    /// <returns>True when it waits on the owner.</returns>
    internal static bool WaitsOnYou(ProjectTruthView? truth) =>
        truth is not null && (truth.BlockedOnYou == true || truth.OpenPrs.Any(p => p.WaitsOnYou == true));

    /// <summary>The Blocked on cell: "you" when the server flags the project or a PR as waiting on the owner, else the first reviewer a PR waits on, else nothing.</summary>
    /// <param name="truth">The project's truth.</param>
    /// <returns>The words; empty for none.</returns>
    internal static string BlockedOn(ProjectTruthView? truth)
    {
        if (truth is null)
        {
            return string.Empty;
        }

        if (WaitsOnYou(truth))
        {
            return Labels.ProjectBlockedOnYou;
        }

        return truth.OpenPrs.Select(p => p.WaitingOn).FirstOrDefault(w => !string.IsNullOrWhiteSpace(w)) is { } who
            ? who.Split(',')[0].Trim()
            : string.Empty;
    }

    /// <summary>The Items cell: "done/total"; empty when the project has no item.</summary>
    /// <param name="truth">The project's truth.</param>
    /// <returns>The words.</returns>
    internal static string Items(ProjectTruthView? truth) =>
        truth is { TotalItems: > 0 } ? Labels.ProjectItems(truth.DoneItems, truth.TotalItems) : string.Empty;

    /// <summary>The Agents cell: the running agents; empty for none.</summary>
    /// <param name="truth">The project's truth.</param>
    /// <returns>The words.</returns>
    internal static string Agents(ProjectTruthView? truth) =>
        truth is { RunningAgents: > 0 } ? truth.RunningAgents.ToString(CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>The target as a day; null for none or one that isn't yyyy-MM-dd.</summary>
    /// <param name="target">The served target.</param>
    /// <returns>The day.</returns>
    internal static DateOnly? TargetDay(string? target) =>
        DateOnly.TryParseExact(target, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    /// <summary>The Target cell: "Dec 7", or "Nov 12 passed" for a day before today; a target that isn't yyyy-MM-dd is shown as served.</summary>
    /// <param name="target">The served target.</param>
    /// <param name="today">Today, from the app's clock.</param>
    /// <returns>The words (empty for none) and whether the day has passed.</returns>
    internal static (string Text, bool Passed) Target(string? target, DateOnly today)
    {
        if (string.IsNullOrEmpty(target))
        {
            return (string.Empty, false);
        }

        if (TargetDay(target) is not { } day)
        {
            return (target, false);
        }

        var words = day.ToString("MMM d", CultureInfo.InvariantCulture);
        return day < today ? (Labels.ProjectTargetPassed(words), true) : (words, false);
    }

    private static List<ProjectRowGroup> ByHealth(IReadOnlyList<ProjectCardView> cards)
    {
        var levels = cards.Select(c => c.Truth?.Health?.Level).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var ordered = _healthOrder.Where(l => levels.Contains(l, StringComparer.Ordinal))
            .Concat(levels.Where(l => !_healthOrder.Contains(l, StringComparer.Ordinal)));   // a level this build doesn't know, as served, before No open PRs
        var groups = ordered
            .Select(level => new ProjectRowGroup(level, Labels.ProjectHealthGroup(level), [.. cards.Where(c => Is(c.Truth?.Health?.Level, level))]))
            .ToList();
        var none = cards.Where(c => c.Truth?.Health?.Level is null).ToList();
        if (none.Count > 0)
        {
            groups.Add(new ProjectRowGroup(ProjectRowGroup.NoOpenPrs, Labels.ProjectsNoOpenPrs, none));
        }

        return groups;
    }

    private static List<ProjectRowGroup> ByState(IReadOnlyList<ProjectCardView> cards)
    {
        var states = cards.Select(c => c.State ?? string.Empty).Distinct(StringComparer.Ordinal).ToList();
        var ordered = _stateOrder.Where(s => states.Contains(s, StringComparer.Ordinal))
            .Concat(states.Where(s => !_stateOrder.Contains(s, StringComparer.Ordinal)));   // any other state in the order the server first serves it
        return [.. ordered.Select(state => new ProjectRowGroup(state, Labels.ProjectStateWord(state), [.. cards.Where(c => Is(c.State ?? string.Empty, state))]))];
    }

    private static List<ProjectRowGroup> ByRepo(IReadOnlyList<ProjectCardView> cards)
    {
        var repos = cards.SelectMany(Repos).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var groups = repos
            .Select(repo => new ProjectRowGroup(repo, repo, [.. cards.Where(c => Repos(c).Contains(repo, StringComparer.Ordinal))]))
            .ToList();
        var none = cards.Where(c => Repos(c).Count == 0).ToList();
        if (none.Count > 0)
        {
            groups.Add(new ProjectRowGroup(ProjectRowGroup.NoOpenPrs, Labels.ProjectsNoOpenPrs, none));
        }

        return groups;
    }

    // The repos of a project's open PRs that are not drafts.
    private static List<string> Repos(ProjectCardView card) =>
        card.Truth is not { } truth ? [] : [.. truth.OpenPrs.Where(p => !Is(p.State, "draft")).Select(p => p.Repo).Distinct(StringComparer.Ordinal)];

    private static bool Is(string? value, string word) => string.Equals(value, word, StringComparison.Ordinal);
}
