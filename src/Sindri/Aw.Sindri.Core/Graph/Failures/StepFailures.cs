namespace Aw.Sindri.Core.Graph.Failures;

/// <summary>Turns a failed node run's error ("&lt;code&gt;: &lt;message&gt;") into its §6.1 sentence and fix, with the step named by its title (#3304).</summary>
public static class StepFailures
{
    /// <summary>The sentence and fix for a failed step.</summary>
    /// <param name="step">The step's title, never its node id.</param>
    /// <param name="error">The node run's error as the engine wrote it.</param>
    /// <param name="raw">The model's text of a failed prompt step, or null.</param>
    /// <returns>The failure in words, or null when the code has no row or the row needs a value the error does not carry.</returns>
    public static StepFailure? Describe(string step, string error, string? raw = null) => throw new NotSupportedException();
}
