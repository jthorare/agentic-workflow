namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>What a fix task did (review page spec §8.2, §16 "Wording the mockups set").</summary>
internal enum ReviewFixOutcome
{
    /// <summary>Not sent.</summary>
    None,

    /// <summary>A babysitter instance watching the PR took the thread.</summary>
    Babysitter,

    /// <summary>A live session on the head branch took it.</summary>
    Session,

    /// <summary>No session: the item waits in Work as "Waiting on &lt;branch&gt;".</summary>
    Waiting,

    /// <summary>A contributor's PR: the item only, nothing posted.</summary>
    ItemOnly,

    /// <summary>The item was made and the post failed (SND-REVIEW-006), with Retry.</summary>
    HalfDone,

    /// <summary>Nothing was made: the refusal's words.</summary>
    Failed,
}
