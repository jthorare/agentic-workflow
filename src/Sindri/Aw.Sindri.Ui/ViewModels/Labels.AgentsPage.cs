using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Agents page alone (collector spec CU-1, CU-2, CU-6). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The page when an answer can't be read.</summary>
    internal const string SessionsUnreadable = "The agent list can't be read from this Sindri.";

    /// <summary>The fold of sessions that ended.</summary>
    internal const string EndedStatus = "Ended";

    internal const string ColumnStatus = "Status";

    internal const string ColumnAgent = "Agent";

    internal const string ColumnWhere = "Where it runs";

    internal const string ColumnDoing = "What it is doing";

    internal const string ColumnFor = "For";

    internal const string RecentlyEnded = "Recently ended";

    /// <summary>An ended session with no known way back.</summary>
    internal const string NoResumeCommand = "no resume command";

    /// <summary>A filter's accessible name: the choice it shows leads, then what it filters.</summary>
    /// <param name="choice">The choice shown.</param>
    /// <param name="what">What the filter narrows by ("provider").</param>
    /// <returns>"All providers, filter by provider".</returns>
    internal static string FilterName(string choice, string what) => $"{choice}, filter by {what}";

    /// <summary>The first choice of the provider filter.</summary>
    internal const string AllProviders = "All providers";

    /// <summary>The first choice of the state filter.</summary>
    internal const string AllStates = "Any status";

    /// <summary>The first choice of the repo filter.</summary>
    internal const string AllRepos = "All repos";

    /// <summary>The Focus control's label (the catalog's action word).</summary>
    internal const string FocusLabel = "Watch the agent";

    /// <summary>The CLI line Focus shows until the send is wired, when the catalog row is missing.</summary>
    internal const string FocusCliFallback = "sindri agents focus <ref>";

    /// <summary>What the pending Watch the agent button says it is, instead of a phase code.</summary>
    internal const string FocusSoon = "Coming soon";

    /// <summary>The pending Watch the agent button's accessible name: its words, then why it is dead.</summary>
    internal static readonly string FocusName = FocusLabel + ", " + FocusSoon.ToLowerInvariant();

    /// <summary>The recently ended fold's header with its count.</summary>
    /// <param name="count">How many ended.</param>
    /// <returns>The words.</returns>
    internal static string RecentlyEndedHeader(int count) => string.Create(CultureInfo.InvariantCulture, $"{RecentlyEnded} ({count})");

    /// <summary>A group header with its count.</summary>
    /// <param name="group">The group's name.</param>
    /// <param name="count">How many rows.</param>
    /// <returns>The words.</returns>
    internal static string GroupHeader(string group, int count) => string.Create(CultureInfo.InvariantCulture, $"{group} ({count})");

    /// <summary>A launched session's line, in place of its launch id (the id is on the session's sheet, under Details).</summary>
    internal const string LaunchLine = "Agent session";

    /// <summary>A lane's line: "Lane sindri/ideas".</summary>
    /// <param name="team">The team.</param>
    /// <param name="lane">The lane.</param>
    /// <returns>The words.</returns>
    internal static string LaneLine(string team, string lane) => "Lane " + team + "/" + lane;

    /// <summary>What a waiting session waits on: "approval · Bash".</summary>
    /// <param name="reason">The served reason.</param>
    /// <param name="tool">The served tool, or null.</param>
    /// <returns>The words.</returns>
    internal static string AskingDetail(string reason, string? tool) => string.IsNullOrEmpty(tool) ? reason : reason + " · " + tool;

    /// <summary>An ended row's time.</summary>
    /// <param name="age">How long ago it ended.</param>
    /// <returns>The words.</returns>
    internal static string EndedAgo(TimeSpan age) => "ended " + Age(age) + " ago";

    /// <summary>A session row's accessible name: provider, title, state, what it asks, host.</summary>
    /// <param name="provider">The provider's label.</param>
    /// <param name="title">The title or its fallback.</param>
    /// <param name="state">The state's word.</param>
    /// <param name="detail">What it asks, or empty.</param>
    /// <param name="host">The host's label.</param>
    /// <returns>The words.</returns>
    internal static string SessionName(string provider, string title, string state, string detail, string host) =>
        string.Join(", ", new[] { provider, title, state, detail, host }.Where(p => p.Length > 0));

    /// <summary>The step an engine launch started a session for: "On a workflow step · build", with "· attempt 2" above the first attempt. The run's address is never up front.</summary>
    /// <param name="state">The Step's state, or null.</param>
    /// <param name="attempt">The attempt, or null.</param>
    /// <returns>The words.</returns>
    internal static string SessionStep(string? state, int? attempt) =>
        string.Join(" · ", new[] { "On a workflow step", state ?? string.Empty, attempt is > 1 ? "attempt " + attempt.Value.ToString(CultureInfo.InvariantCulture) : string.Empty }.Where(p => p.Length > 0));

    /// <summary>The step button's accessible name: its visible words, then what it opens ("On a workflow step · build: open the run at build").</summary>
    /// <param name="visible">The button's visible words.</param>
    /// <param name="state">The Step's state, or null.</param>
    /// <returns>The words.</returns>
    internal static string SessionStepName(string visible, string? state) =>
        state is { Length: > 0 } ? $"{visible}: open the run at {state}" : $"{visible}: open the run";
}
