namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The Workflows page's words (<see cref="Labels"/>, continued): a check of the open workflow that failed (#4380).</summary>
internal static partial class Labels
{
    /// <summary>The line over the graph when the server's validation could not answer; Retry asks again, and so does opening the workflow again.</summary>
    internal const string WfFindingsFailed = "Couldn't check this workflow for problems.";

    /// <summary>The failed-check line's button: asks validate again for the shown revision.</summary>
    internal const string WfFindingsRetry = "Retry";
}
