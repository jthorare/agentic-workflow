namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What closed a request from the action bar.</summary>
internal enum NeedsYouBarOutcome
{
    /// <summary>Not closed.</summary>
    None,

    /// <summary>Approved.</summary>
    Approve,

    /// <summary>Sent back with a reason.</summary>
    Revise,

    /// <summary>A step only a terminal completes, sent back with a reason (the step's own outcome for it).</summary>
    Rework,

    /// <summary>Dropped from the card's More menu.</summary>
    Drop,

    /// <summary>The ask step's answers sent (the step's outcome "done").</summary>
    Answer,

    /// <summary>One round of the scoping interview answered (the step's outcome "answered").</summary>
    Answered,

    /// <summary>The scoping interview stopped with what has been answered (the step's outcome "doneAsking").</summary>
    DoneAsking,
}
