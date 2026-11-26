namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the frame around a failed run's sentence (#3304). The sentence and the fix are Sindri's, never built here.</summary>
internal static partial class Labels
{
    /// <summary>The heading over the engine's own text of a failed run.</summary>
    internal const string WfFailureDetails = "Details";

    /// <summary>A failed run's heading.</summary>
    /// <param name="step">The failed step's title, as served.</param>
    /// <returns>The words.</returns>
    internal static string WfFailedAt(string step) => "Failed at " + step;

    /// <summary>A failed run header's accessible name.</summary>
    /// <param name="heading">"Failed at Check".</param>
    /// <param name="sentence">Sindri's sentence.</param>
    /// <returns>The words.</returns>
    internal static string WfFailureName(string heading, string sentence) => heading + ". " + sentence;
}
