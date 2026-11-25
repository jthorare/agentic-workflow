namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The Workflows page's words (<see cref="Labels"/>, continued): a check of the open workflow that failed (#4380).</summary>
internal static partial class Labels
{
    /// <summary>The line over the graph when the server's validation could not answer; the next open asks again.</summary>
    internal const string WfFindingsFailed = "Couldn't check this workflow for problems. It checks again when you open it.";
}
