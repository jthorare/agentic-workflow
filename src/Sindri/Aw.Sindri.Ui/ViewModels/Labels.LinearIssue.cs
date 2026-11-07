using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Linear issue sheet and Home's Linear line (Linear hub spec §3).</summary>
internal static partial class Labels
{
    /// <summary>The sheet before its first answer.</summary>
    internal const string LinearIssueLoading = "Loading…";

    /// <summary>A field the issue has no value for.</summary>
    internal const string LinearFieldNone = "–";

    /// <summary>The "Open in Linear" button.</summary>
    internal const string LinearOpenIn = "Open in Linear";

    /// <summary>The sheet's sentence for a key Sindri has not stored.</summary>
    /// <param name="key">The key.</param>
    /// <returns>"ENG-99 isn't synced yet".</returns>
    internal static string LinearIssueNotSynced(string key) => $"{key} isn't synced yet";

    /// <summary>The note on an issue Linear no longer serves.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The note.</returns>
    internal static string LinearIssueRemoved(string key) => $"{key} is no longer served by Linear. This is the last copy Sindri stored.";

    /// <summary>A field's label in the grid.</summary>
    /// <param name="field">state, assignee, priority, project, cycle or labels.</param>
    /// <returns>The label.</returns>
    internal static string LinearIssueField(string field) => field switch
    {
        "state" => "State",
        "assignee" => "Assignee",
        "priority" => "Priority",
        "project" => "Project",
        "cycle" => "Cycle",
        _ => "Labels",
    };

    /// <summary>The heading over the description.</summary>
    internal const string LinearIssueDescription = "Description";

    /// <summary>The heading over the comments.</summary>
    /// <param name="count">How many comments.</param>
    /// <returns>"Comments (2)".</returns>
    internal static string LinearIssueComments(int count) => string.Create(CultureInfo.InvariantCulture, $"Comments ({count})");

    /// <summary>The line when the issue has no description.</summary>
    internal const string LinearIssueNoDescription = "No description";

    /// <summary>The mark on a comment Sindri wrote.</summary>
    internal const string LinearFromSindri = "Sindri";

    /// <summary>The PR chip's accessible name.</summary>
    /// <param name="text">The chip's text.</param>
    /// <returns>"PR #2071 waiting on you, open its review".</returns>
    internal static string LinearPrChipOpens(string text) => text + ", open its review";

    /// <summary>A picture in a description or comment, which the app does not fetch.</summary>
    /// <param name="alt">The picture's text.</param>
    /// <param name="host">The host it would load from, or empty.</param>
    /// <returns>"[image p95 from example.com]".</returns>
    internal static string LinearImagePlaceholder(string alt, string host) =>
        "[image" + (alt.Length > 0 ? " " + alt : string.Empty) + (host.Length > 0 ? " from " + host : string.Empty) + "]";

    /// <summary>A named link: the name, then the host it goes to, so a name cannot hide where the link leads.</summary>
    /// <param name="name">The link's name.</param>
    /// <param name="host">The host.</param>
    /// <returns>"the runbook (example.com)".</returns>
    internal static string LinearNamedLink(string name, string host) => $"{name} ({host})";

    /// <summary>Home's line and the tray's.</summary>
    /// <param name="count">How many changes the strip holds.</param>
    /// <returns>"3 new on your Linear issues".</returns>
    internal static string LinearNewLine(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} new on your Linear issues");
}
