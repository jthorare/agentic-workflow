namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): how a run ends, the failed, didn't-work-out and cancelled states (F3 spec 3 §8.5). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The heading of the block that says why a step failed.</summary>
    internal const string RunWhatWentWrong = "What went wrong";

    /// <summary>The step could not be started.</summary>
    internal const string RunReasonLaunch = "The agent could not be started.";

    /// <summary>The step's session went away.</summary>
    internal const string RunReasonLost = "The agent's session was lost before it finished.";

    /// <summary>The step's agent stopped on its own.</summary>
    internal const string RunReasonExit = "The agent stopped before it finished.";

    /// <summary>The step used up its allowance.</summary>
    internal const string RunReasonLimit = "The agent ran out of its allowance.";

    /// <summary>"Ended 3h 10m ago", for a run whose start is not served.</summary>
    /// <param name="ago">How long ago, as <see cref="Duration(long)"/> says it.</param>
    /// <returns>The words.</returns>
    internal static string RunEndedAgo(string ago) => $"Ended {ago} ago";

    /// <summary>The words of a step's failure reason; a reason this build does not know shows as served.</summary>
    /// <param name="reason">The reason: launch, lost, exit, limit or a free text.</param>
    /// <returns>The sentence.</returns>
    internal static string RunReason(string reason) => reason switch
    {
        "launch" => RunReasonLaunch,
        "lost" => RunReasonLost,
        "exit" => RunReasonExit,
        "limit" => RunReasonLimit,
        _ => reason,
    };
}
