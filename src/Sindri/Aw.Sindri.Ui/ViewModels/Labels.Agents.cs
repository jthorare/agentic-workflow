using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the words the Agents page and the session sheet share (collector spec CU-1, CU-6). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The group of sessions no workflow owns (CO12).</summary>
    internal const string OutsideWorkflow = "Not part of a workflow";

    /// <summary>The group of sessions Sindri launched, until recipe states group them (Q1).</summary>
    internal const string StartedBySindri = "Started by Sindri";

    /// <summary>The pinned group of the asking rows (#3082).</summary>
    internal const string NeedsYouGroup = "Needs you";

    /// <summary>The page's footer: what the list can't show.</summary>
    internal const string CursorChatsNotShown = "Cursor IDE chats aren't shown";

    /// <summary>The page while the first read is pending.</summary>
    internal const string ReadingSessions = "Reading sessions…";

    /// <summary>The sheet while its first read is pending.</summary>
    internal const string ReadingSession = "Reading the session…";

    /// <summary>The page when a filter leaves no row (the served sentence is for an empty list).</summary>
    internal const string NoSessionsMatch = "No sessions match these filters.";

    /// <summary>The sheet when its session is gone.</summary>
    internal const string SessionEnded = "This session has ended.";

    /// <summary>The page when the collector can't run here (SND-SESSION-002); the detail is the served word.</summary>
    /// <param name="detail">The served detail (disabled, proc-unreadable, unsupported-platform).</param>
    /// <returns>The line.</returns>
    internal static string SessionsUnavailable(string detail) => $"Sindri can't see agent sessions on this system: {detail}";

    /// <summary>The head: "Agents  N live · M asking · K outside a workflow" (the title is the page's own).</summary>
    /// <param name="live">Live sessions.</param>
    /// <param name="asking">Sessions waiting on the human.</param>
    /// <returns>The counts line.</returns>
    internal static string AgentsHead(int live, int asking) => AgentsCounts(live, asking);

    /// <summary>The live region's text when the counts change, and the tray's line: "N agents · M asking".</summary>
    /// <param name="live">Live sessions.</param>
    /// <param name="asking">Sessions waiting on the human.</param>
    /// <returns>The line.</returns>
    internal static string AgentsCounts(int live, int asking) =>
        string.Create(CultureInfo.InvariantCulture, $"{live} {(live == 1 ? "agent" : "agents")} · {asking} asking you");
}
