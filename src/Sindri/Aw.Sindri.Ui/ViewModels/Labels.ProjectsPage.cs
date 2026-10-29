using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Projects page's view models (projects page spec §3, §3.1). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The page's name.</summary>
    internal const string ProjectsPageTitle = "Projects";

    /// <summary>The page before its first answer.</summary>
    internal const string ReadingProjects = "Reading projects…";

    /// <summary>The finished fold's title.</summary>
    internal const string ProjectsFinishedTitle = "Finished";

    /// <summary>The finished fold's reason line.</summary>
    internal const string ProjectsFinishedWhy = "Done or canceled in the last 14 days";

    /// <summary>The hidden fold's title.</summary>
    internal const string ProjectsHiddenTitle = "Hidden";

    /// <summary>A card of a project the store no longer holds.</summary>
    internal const string ProjectGone = "no longer synced from Linear";

    /// <summary>The page width from which cards sit two across.</summary>
    internal const double ProjectsTwoColumnsFrom = 560;

    /// <summary>The page width from which cards sit three across (the mockup README moved it from 900 so names aren't cut at a 1,000 px window).</summary>
    internal const double ProjectsThreeColumnsFrom = 1040;

    /// <summary>The longest note the card's face shows, in characters.</summary>
    internal const int ProjectNoteFaceMax = 80;

    /// <summary>The page when a read failed: the reason follows.</summary>
    internal static string ProjectsFailed(Error error) => "Couldn't read the projects just now. " + InstanceRefused(error);

    /// <summary>One source's name in the freshness line.</summary>
    internal static string ProjectSource(string source) => source switch
    {
        "linear-projects" => "Linear projects",
        "linear" => "Linear issues",
        "github" => "GitHub",
        "context" => "Context",
        _ => source,
    };

    /// <summary>The freshness line's last part.</summary>
    internal static string ProjectViewer(string viewer) => "Linear says you're " + viewer;

    /// <summary>A project state's word; a state this build doesn't know is shown as served, first letter up.</summary>
    internal static string ProjectStateWord(string? state) => state switch
    {
        null or "" => "No state",
        "inProgress" => "In progress",
        "planned" => "Planned",
        "paused" => "Paused",
        "backlog" => "Backlog",
        "done" => "Done",
        "canceled" or "cancelled" => "Canceled",
        _ => char.ToUpperInvariant(state[0]) + state[1..],
    };

    /// <summary>A health's word; null for one this build doesn't know.</summary>
    internal static string? ProjectHealthWord(string? health) => health switch
    {
        "onTrack" => "on track",
        "atRisk" => "at risk",
        "offTrack" => "off track",
        _ => null,
    };

    /// <summary>The origin chip; null for a project the owner leads in Linear.</summary>
    internal static string? ProjectOriginChip(string origin) => origin switch
    {
        "attached" => "attached",
        "handMade" => "by hand",
        _ => null,
    };

    /// <summary>"MMM d" in the invariant culture.</summary>
    internal static string ProjectDate(DateTimeOffset date) => date.UtcDateTime.ToString("MMM d", CultureInfo.InvariantCulture);

    /// <summary>"Done Oct 23" or "Canceled Oct 18".</summary>
    internal static string ProjectFinishedOn(string? state, DateTimeOffset at) => ProjectStateWord(state) + " " + ProjectDate(at);

    /// <summary>"Lead: you" or "Lead: Sam Lee".</summary>
    internal static string ProjectLead(bool you, string? name) => "Lead: " + (you ? "you" : name ?? "unknown");

    /// <summary>"Target Jan 31"; a date that isn't yyyy-MM-dd is shown as served.</summary>
    internal static string ProjectTarget(string target) =>
        "Target " + (DateOnly.TryParseExact(target, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day.ToString("MMM d", CultureInfo.InvariantCulture) : target);

    /// <summary>"2h ago", or "just now".</summary>
    internal static string ProjectActive(TimeSpan age) => AgoWords(age);

    /// <summary>"12 open issues".</summary>
    internal static string ProjectOpenIssues(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} open {(count == 1 ? "issue" : "issues")}");

    /// <summary>"2 open PRs".</summary>
    internal static string ProjectOpenPrs(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} open {(count == 1 ? "PR" : "PRs")}");
}
