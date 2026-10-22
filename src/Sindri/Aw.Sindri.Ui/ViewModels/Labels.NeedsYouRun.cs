using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): a paused run's answer form and the takeover (F3 needs-you spec §6, slice 4).</summary>
internal static partial class Labels
{
    /// <summary>The caption over a paused run's answer options.</summary>
    internal const string NeedsYouAnswerCaption = "Your answer";

    /// <summary>The option that gives the run more tries (its button is the terminal's: the grant is protected).</summary>
    internal const string NeedsYouMoreTriesHelp = "The AI reviews again. Pick how many extra tries below.";

    /// <summary>The option that gives the run a hint.</summary>
    internal const string NeedsYouHintOption = "Give a hint first";

    /// <summary>Its one-line help.</summary>
    internal const string NeedsYouHintHelp = "Tell the AI what to focus on, then it tries again.";

    /// <summary>The option that ends the run.</summary>
    internal const string NeedsYouStopOption = "Stop this run";

    /// <summary>Its one-line help.</summary>
    internal const string NeedsYouStopHelp = "Ends the run. You can start it again later.";

    /// <summary>The caption of the extra-tries stepper.</summary>
    internal const string NeedsYouExtraTries = "Extra tries";

    /// <summary>The stepper's more button name.</summary>
    internal const string NeedsYouExtraTriesMore = "One more extra try";

    /// <summary>The stepper's fewer button name.</summary>
    internal const string NeedsYouExtraTriesFewer = "One fewer extra try";

    /// <summary>The hint box's placeholder.</summary>
    internal const string NeedsYouHintPlaceholder = "What should the AI focus on?";

    /// <summary>The answer form's send button.</summary>
    internal const string NeedsYouSendAnswer = "Send answer";

    /// <summary>The line after an answer was taken.</summary>
    internal const string NeedsYouAnswerSent = "Answer sent. The run carries on.";

    /// <summary>The line after the more-tries command was copied.</summary>
    internal const string NeedsYouAnswerCopied = "Copied. Paste it in your terminal to allow more tries.";

    /// <summary>The badge, node footer and step row of a run the user took over.</summary>
    internal const string NeedsYouTookOver = "You took over";

    /// <summary>The badge of a run handed back.</summary>
    internal const string NeedsYouRunning = "Running";

    /// <summary>The footer of a taken-over run: the clock stands still.</summary>
    /// <param name="left">The time left, as the engine says it.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouClockPaused(string left) => $"clock paused, {left} left";

    /// <summary>The footer of a run handed back: the clock runs again.</summary>
    /// <param name="left">The time left, as the engine says it.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouClockRunning(string left) => $"clock running, {left} left";

    /// <summary>The footer of a taken-over run whose time left is not served.</summary>
    internal const string NeedsYouClockPausedNoTime = "clock paused";

    /// <summary>The inspector's hand-back box.</summary>
    /// <param name="left">The time left, as the engine says it; empty when it is not served.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouHandBackBox(string left) => left.Length == 0
        ? "Hand back when you are done. The workflow carries on from here."
        : $"Hand back when you are done. The workflow carries on from here, and the clock restarts with the {left} that was left.";

    /// <summary>The hand back button.</summary>
    internal const string NeedsYouHandBack = "Hand back";

    /// <summary>The cancel button of the inspector.</summary>
    internal const string NeedsYouCancelRun = "Cancel run";

    /// <summary>Its help.</summary>
    internal const string NeedsYouCancelRunHelp = "Stops this step and the stages that wait on it.";

    /// <summary>The nudge's title.</summary>
    /// <param name="hours">Whole hours the run has been with the user.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouNudgeTitle(int hours) => string.Create(CultureInfo.InvariantCulture, $"You've had this for {hours} hours: hand it back?");

    /// <summary>The nudge's body.</summary>
    internal const string NeedsYouNudgeBody = "The clock on this step stays paused until you do. The workflow carries on from here.";

    /// <summary>The nudge's dismiss button.</summary>
    internal const string NeedsYouNotYet = "Not yet";

    /// <summary>The quiet toast after a hand back.</summary>
    internal const string NeedsYouHandedBack = "Handed back";

    /// <summary>The toast's body.</summary>
    internal const string NeedsYouHandedBackBody = "The workflow carries on from here.";

    /// <summary>The back-out button of the cancel question.</summary>
    internal const string NeedsYouBackOut = "Keep it";

    /// <summary>The inspector's line saying when the user took the run over.</summary>
    /// <param name="time">The time of day, "14:05".</param>
    /// <param name="ago">How long ago, as the engine says a length of time.</param>
    /// <returns>The words.</returns>
    internal static string NeedsYouTookOverAt(string time, string ago) => $"you took over at {time} ({ago} ago)";
}
