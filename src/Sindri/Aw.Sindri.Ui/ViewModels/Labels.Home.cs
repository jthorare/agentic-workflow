namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): Home's empty lines when the API served no sentence (F3 home spec §3).</summary>
internal static partial class Labels
{
    /// <summary>Needs you, nothing waiting.</summary>
    internal const string HomeNothingNeedsYou = "Nothing needs you.";

    /// <summary>A stalled Running card's body, in place of the banner's sentence.</summary>
    internal const string QueueNothingRunning = "Nothing running.";

    /// <summary>A stalled Queued card's body, in place of the banner's sentence.</summary>
    internal const string QueueNothingQueued = "Nothing queued.";

    /// <summary>"Nothing running. Start a workflow to see it here."</summary>
    internal const string HomeNothingRunning = "Nothing running. Start a workflow to see it here.";

    /// <summary>The Retry button of a failed or stale read (<see cref="Controls.FailedRead"/>).</summary>
    internal const string Retry = "Retry";

    /// <summary>Home's Running and Queued panels and the Queue page when the first queue read failed (same words as <see cref="PageReadFailed"/>).</summary>
    internal const string QueueReadFailed = "Queue can't be read right now.";

    /// <summary>The disclosure at the end of a failed-read line that opens the code.</summary>
    internal const string FailedReadDetails = "Details ▸";

    /// <summary>The words of a page whose read failed: "Queue can't be read right now." (first-run spec §4.6).</summary>
    /// <param name="page">The page's name.</param>
    /// <returns>The sentence.</returns>
    internal static string PageReadFailed(string page) => page + " can't be read right now.";
}
