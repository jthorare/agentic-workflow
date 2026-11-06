using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the findings viewer (model-check design §4.3).</summary>
internal static partial class Labels
{
    /// <summary>What the findings viewer says for a list with nothing in it.</summary>
    internal const string FindingsNone = "No findings";

    /// <summary>The accessible name of a violation's list of steps.</summary>
    internal const string FindingSteps = "Steps";

    /// <summary>The Full trace button's name: the number of states in the trace behind it.</summary>
    /// <param name="states">The number of states.</param>
    /// <returns>"Full trace, 4 states" ("Full trace, 1 state").</returns>
    internal static string FullTraceName(int states) => states == 1 ? "Full trace, 1 state" : $"Full trace, {states.ToString(CultureInfo.InvariantCulture)} states";

    /// <summary>What assistive tech reads for a finding: its message, then where it is.</summary>
    /// <param name="message">The message.</param>
    /// <param name="where">The path, line and rule; empty for none.</param>
    /// <returns>The words.</returns>
    internal static string FindingName(string message, string where) => where.Length > 0 ? $"{message}, {where}" : message;
}
