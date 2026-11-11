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
}
