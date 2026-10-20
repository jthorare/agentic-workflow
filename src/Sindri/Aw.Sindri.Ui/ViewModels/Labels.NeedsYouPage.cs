using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Needs-you inbox page (F3 needs-you spec §6).</summary>
internal static partial class Labels
{
    /// <summary>The inbox's title.</summary>
    internal const string NeedsYouInboxTitle = "Needs you";

    /// <summary>The breadcrumb's first word, a button back to Home.</summary>
    internal const string NeedsYouCrumbHome = "Home";

    /// <summary>The breadcrumb's separator.</summary>
    internal const string NeedsYouCrumbSeparator = ">";

    /// <summary>The breadcrumb button's accessible name.</summary>
    internal const string NeedsYouCrumbHomeName = "Home, back to Home";

    /// <summary>The header's count.</summary>
    /// <param name="waiting">How many rows wait for the user.</param>
    /// <param name="runs">How many of them are paused runs.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouWaiting(int waiting, int runs) => string.Create(CultureInfo.InvariantCulture, $"{waiting} waiting, {runs} {(runs == 1 ? "run" : "runs")}");

    /// <summary>The heading of the desk tasks.</summary>
    internal const string NeedsYouGroupApprovals = "Your approvals";

    /// <summary>The heading of the paused runs.</summary>
    internal const string NeedsYouGroupRuns = "Paused runs";

    /// <summary>The heading of the agent decisions.</summary>
    internal const string NeedsYouGroupDecisions = "Agent decisions";

    /// <summary>The heading of the workflow and settings approvals.</summary>
    internal const string NeedsYouGroupSettings = "Workflow and settings approvals";

    /// <summary>The heading of the failures, which the prototype leaves out and the app keeps so no row is lost.</summary>
    internal const string NeedsYouGroupFailures = "Something failed";

    /// <summary>The heading of a row whose kind the list serves empty.</summary>
    internal const string NeedsYouGroupOther = "Other";

    /// <summary>A group header's accessible name: the heading and how many rows it holds.</summary>
    /// <param name="heading">The group's heading.</param>
    /// <param name="count">Its rows.</param>
    /// <returns>The name.</returns>
    internal static string NeedsYouGroupName(string heading, string count) => $"{heading}, {count}";

    /// <summary>The card's kind word for a desk task.</summary>
    internal const string NeedsYouKindApproval = "Your approval";

    /// <summary>The card's kind word for a paused run.</summary>
    internal const string NeedsYouKindRun = "Paused run";

    /// <summary>The card's kind word for an agent's decision.</summary>
    internal const string NeedsYouKindDecision = "Agent decision";

    /// <summary>The card's kind word for a changed workflow.</summary>
    internal const string NeedsYouKindWorkflow = "Workflow change";

    /// <summary>The card's kind word for changed settings.</summary>
    internal const string NeedsYouKindSettings = "Settings change";

    /// <summary>The card's kind word for a failure.</summary>
    internal const string NeedsYouKindFailure = "Something failed";

    /// <summary>The word a selected inbox row adds to its accessible name.</summary>
    internal const string NeedsYouSelectedWord = "selected";

    /// <summary>The pill that holds new rows back while the user reads.</summary>
    /// <param name="count">How many new rows are held.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouNewPill(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} new {(count == 1 ? "request" : "requests")}, held while you read");

    /// <summary>The key hint's J and K, and what they do.</summary>
    internal const string NeedsYouKeyMoveWord = "move";

    /// <summary>The caption of the card's first section.</summary>
    internal const string NeedsYouWhyCaption = "Why this needs you";

    /// <summary>The caption of a row's detail lines.</summary>
    internal const string NeedsYouRequestCaption = "The request";

    /// <summary>The link from the card's head to the item the request is about.</summary>
    internal const string NeedsYouOpenItem = "Open the item";

    /// <summary>The button that copies the card's reference.</summary>
    internal const string NeedsYouCopyReference = "Copy reference";

    /// <summary>The card's footer, left.</summary>
    internal const string NeedsYouFootWaiting = "Waiting for you";

    /// <summary>The card's footer, right.</summary>
    /// <param name="waited">How long the row has waited, as <see cref="NeedsYouWaited"/> words it.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouFootRight(string waited) => "waiting " + waited;

    /// <summary>How long a row has waited, to the minute: "26m", "5h 51m", "6d 18h".</summary>
    /// <param name="age">How long.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouWaited(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "<1m",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes}m"),
        < 1440 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours}h {age.Minutes}m"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays}d {age.Hours}h"),
    };

    /// <summary>The detail column's sentence when the list is empty and serves none.</summary>
    internal const string NeedsYouEmptyFallback = "Nothing needs you";

    /// <summary>The card's accessible name: the kind word and the title.</summary>
    /// <param name="kind">The kind word.</param>
    /// <param name="title">The row's title.</param>
    /// <returns>The name.</returns>
    internal static string NeedsYouCardName(string kind, string title) => $"{kind}: {title}";
}
