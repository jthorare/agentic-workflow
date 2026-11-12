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

    /// <summary>Home's Running and Queued panels when the first queue read failed.</summary>
    internal const string QueueReadFailed = "Couldn't read the queue just now.";
}
