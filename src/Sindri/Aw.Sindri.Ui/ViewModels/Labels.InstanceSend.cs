namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): what the instance frame says after it sent a command. No em dashes, no internal terms.</summary>
internal static partial class Labels
{
    /// <summary>Said when Cancel was accepted.</summary>
    internal const string CancelSent = "Cancel sent.";

    /// <summary>Said when Hand back was accepted.</summary>
    internal const string HandBackSent = "Handed back.";

    /// <summary>Said when the desk report was accepted.</summary>
    internal const string CompleteSent = "Reported.";

    /// <summary>Said on a row while its command is out.</summary>
    internal const string RowSending = "Sending...";

    /// <summary>Said on a row when its approval went through.</summary>
    internal const string RowApprovalSent = "Approved.";

    /// <summary>Said on a row when its command was accepted.</summary>
    internal const string RowSent = "Sent.";

    /// <summary>Said on a desk row or sheet when the task asks for a choice the app does not make for the person.</summary>
    internal const string CompleteNeedsChoice = "This task needs your choice: open it in Needs you.";

    /// <summary>Said when the desk task could not be read, so nothing was sent.</summary>
    internal const string CompleteUnreadable = "That task can't be read right now. Nothing was sent.";

    /// <summary>Said on a row whose action the app cannot send yet; the command stands in for it.</summary>
    /// <param name="cli">The row's command, or empty.</param>
    /// <returns>The sentence.</returns>
    internal static string RowNotAvailable(string cli) => cli.Length > 0 ? $"Not available here yet. Run: {cli}" : "Not available here yet.";
}

