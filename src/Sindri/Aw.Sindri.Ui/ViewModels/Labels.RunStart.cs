namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): a run Run now started that never showed up (#3702).</summary>
internal static partial class Labels
{
    /// <summary>A Starting placeholder that got no run row for 10 s.</summary>
    internal const string WfRunDidNotStart = "The run didn't start.";

    /// <summary>What to do about a run that didn't start, under that line.</summary>
    internal const string WfRunDidNotStartHint = "Retry, or open System if it keeps happening.";

    /// <summary>The Retry button on that row.</summary>
    internal const string WfRunStartRetry = "Retry";
}
