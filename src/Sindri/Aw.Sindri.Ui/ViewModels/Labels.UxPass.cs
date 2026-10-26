using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): words added by the UX pass. Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The board's lane for open runs of workflows that are not the line.</summary>
    internal const string WorkflowsLaneOther = "Not on a line";

    /// <summary>The Agents head while a filter is on: "2 of 7 agents · 1 asking you" (shown, served live, asking among the shown).</summary>
    /// <param name="shown">Agents the filters keep.</param>
    /// <param name="live">Live sessions served.</param>
    /// <param name="asking">Shown sessions waiting on the human.</param>
    /// <returns>The counts line.</returns>
    internal static string AgentsFilteredHead(int shown, int live, int asking) =>
        string.Create(CultureInfo.InvariantCulture, $"{shown} of {live} {(live == 1 ? "agent" : "agents")} · {asking} asking you");
}
