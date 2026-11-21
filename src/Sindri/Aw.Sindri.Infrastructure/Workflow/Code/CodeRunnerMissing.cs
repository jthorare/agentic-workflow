using System.Globalization;

namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>The one sentence a Code step fails with when its runner binary is not there.</summary>
internal static class CodeRunnerMissing
{
    /// <summary>Names the path and the fix.</summary>
    /// <param name="runnerPath">The runner binary the step looked for.</param>
    /// <returns>'Runner binary not found at &lt;path&gt;. Install it beside the Sindri host.'</returns>
    public static string Message(string runnerPath) =>
        string.Create(CultureInfo.InvariantCulture, $"Runner binary not found at {runnerPath}. Install it beside the Sindri host.");
}
