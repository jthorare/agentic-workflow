namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the buttons of a recipe's verbs on a desk request (the pull request babysitter's stop). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The words of a recipe verb's button; a verb the app has no words for is shown as served.</summary>
    /// <param name="verb">The verb as the instance serves it.</param>
    /// <returns>The button's words.</returns>
    internal static string VerbLabel(string verb) => verb switch
    {
        "comments" => "Held comments",
        "resync-docs" => "Resync docs",
        "check" => "Check now",
        "rework" => "Send back to rework",
        "merge" => "Merge",
        _ => verb,
    };

    /// <summary>The one-line question before a destructive verb.</summary>
    /// <param name="verb">The verb as the instance serves it.</param>
    /// <returns>The question.</returns>
    internal static string VerbConfirmQuestion(string verb) => verb switch
    {
        "merge" => "Merge this pull request? It cannot be undone.",
        _ => VerbLabel(verb) + "? It cannot be undone.",
    };

    /// <summary>The confirm button of the question.</summary>
    internal const string VerbConfirm = "Confirm";

    /// <summary>The button that keeps things as they are.</summary>
    internal const string VerbKeep = "Keep it";

    /// <summary>A verb button's accessible name.</summary>
    /// <param name="words">The button's words.</param>
    /// <param name="subject">The request.</param>
    /// <returns>"Check now ITM-…".</returns>
    internal static string VerbName(string words, string subject) => subject.Length > 0 ? words + " " + subject : words;

    /// <summary>What the bar says after a verb that is not the merge was sent.</summary>
    /// <param name="words">The button's words.</param>
    /// <returns>"Check now: sent".</returns>
    internal static string VerbSent(string words) => words + ": sent";
}
