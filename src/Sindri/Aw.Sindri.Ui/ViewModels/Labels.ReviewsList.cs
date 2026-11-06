using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Reviews tab and its list (review page spec §6.1, §16).</summary>
internal static partial class Labels
{
    /// <summary>The ninth tab's label.</summary>
    internal const string ReviewsTab = "Reviews";

    /// <summary>The Reviews tab's label below 1,100 px (the screen-reader name keeps "Reviews").</summary>
    internal const string ReviewsTabShort = "PRs";

    /// <summary>The Workflows tab's label below 1,100 px (the screen-reader name keeps "Workflows").</summary>
    internal const string WorkflowsTabShort = "Flows";

    /// <summary>The page's name.</summary>
    internal const string ReviewsPageTitle = "Reviews";

    /// <summary>The page before its first answer.</summary>
    internal const string ReadingReviews = "Reading your PRs…";

    /// <summary>The word beside a project section's name.</summary>
    internal const string ReviewsProjectKind = "project";

    /// <summary>A draft PR's chip.</summary>
    internal const string ReviewsDraft = "draft";

    /// <summary>A PR the owner opened.</summary>
    internal const string ReviewsYou = "you";

    /// <summary>CI passed.</summary>
    internal const string ReviewsCiPassing = "passing";

    /// <summary>CI failed.</summary>
    internal const string ReviewsCiFailing = "failing";

    /// <summary>CI is running.</summary>
    internal const string ReviewsCiRunning = "running";

    /// <summary>No CI ran.</summary>
    internal const string ReviewsCiNone = "no CI";

    /// <summary>The PR names a design doc.</summary>
    internal const string ReviewsClaimsDoc = "claims a doc";

    /// <summary>The PR names no design doc.</summary>
    internal const string ReviewsNoDoc = "no doc named";

    /// <summary>The login prompt's Save button.</summary>
    internal const string ReviewsLoginSave = "Save";

    /// <summary>The login field's name.</summary>
    internal const string ReviewsLoginFieldName = "GitHub login";

    /// <summary>The hint line under the list.</summary>
    internal const string ReviewsHint = "J K next / previous PR · Enter open · G V go to Reviews";

    /// <summary>The first column's heading.</summary>
    internal const string ReviewsColPr = "PULL REQUEST";

    /// <summary>The author column's heading.</summary>
    internal const string ReviewsColAuthor = "AUTHOR";

    /// <summary>The age column's heading.</summary>
    internal const string ReviewsColAge = "AGE";

    /// <summary>The CI column's heading.</summary>
    internal const string ReviewsColCi = "CI";

    /// <summary>The required checks column's heading.</summary>
    internal const string ReviewsColRequired = "REQUIRED CHECKS";

    /// <summary>The design doc column's heading.</summary>
    internal const string ReviewsColDoc = "DESIGN DOC";

    /// <summary>The library errors sheet's title.</summary>
    internal const string LibrarySheetTitle = "Library errors";

    /// <summary>The sheet's first section: docs stored with errors.</summary>
    internal const string LibraryBrokenTitle = "Docs with errors";

    /// <summary>The sheet's second section: files that almost read as docs.</summary>
    internal const string LibraryNearMissTitle = "Near misses";

    /// <summary>The sheet's third section: folders that couldn't be read.</summary>
    internal const string LibraryRootsTitle = "Folders that can't be read";

    /// <summary>The sheet when the library has nothing to fix.</summary>
    internal const string LibraryClean = "Nothing to fix.";

    /// <summary>The header's source part: "GitHub synced 14 min ago".</summary>
    internal static string ReviewsSource(string text) => "GitHub " + text;

    /// <summary>The header's login part.</summary>
    internal static string ReviewsAs(string login) => "reviews as " + login;

    /// <summary>The header's count part: "4 open PRs in 2 repos".</summary>
    internal static string ReviewsOpenIn(int prs, int repos) =>
        string.Create(CultureInfo.InvariantCulture, $"{prs} open {(prs == 1 ? "PR" : "PRs")} in {repos} {(repos == 1 ? "repo" : "repos")}");

    /// <summary>The tab's screen-reader name once PRs wait: "Reviews, 2 waiting on you".</summary>
    internal static string ReviewsBadgeName(string label, string count) => $"{label}, {count} waiting on you";

    /// <summary>Home's line: "2 PRs waiting on you".</summary>
    internal static string ReviewsWaitingLine(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "PR" : "PRs")} waiting on you");

    /// <summary>A PR's age, floored: "12 min", "5 h", "2 d".</summary>
    internal static string ReviewsAge(TimeSpan age) =>
        age.TotalHours < 1 ? string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, (int)age.TotalMinutes)} min")
        : age.TotalDays < 1 ? string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours} h")
        : string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays} d");

    /// <summary>The page when a read failed: the reason follows.</summary>
    internal static string ReviewsFailed(Error error) => "Couldn't read your PRs just now. " + InstanceRefused(error);
}
