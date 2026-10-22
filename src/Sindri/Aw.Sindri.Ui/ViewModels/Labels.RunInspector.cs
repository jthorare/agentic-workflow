namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the run canvas's inspector (F3 spec 3 §6). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The heading of the why block.</summary>
    internal const string RunWhy = "Why this step matters";

    /// <summary>The why block while nothing writes it down.</summary>
    internal const string RunWhyNotServed = "Why this step matters isn't written down yet.";

    /// <summary>The heading of the inputs list.</summary>
    internal const string RunNeeds = "What it needs";

    /// <summary>The heading of the outputs list.</summary>
    internal const string RunProduced = "What it produced";

    /// <summary>The heading of the timeline.</summary>
    internal const string RunTimeline = "Step timeline";

    /// <summary>The note under the facts' heading: the lists are the stage's, not the step's.</summary>
    internal const string RunFactsWholeStage = "For the whole stage";

    /// <summary>A fact that is there.</summary>
    internal const string FactReady = "Ready";

    /// <summary>A fact being made.</summary>
    internal const string FactInProgress = "In progress";

    /// <summary>An input that is not there yet.</summary>
    internal const string FactNotYet = "Not yet";

    /// <summary>An output that was not made.</summary>
    internal const string FactNotProduced = "Not produced";

    /// <summary>The line of a step that has no run record.</summary>
    internal const string RunNoTimes = "Its start and end times aren't recorded.";

    /// <summary>The line of a step the run has not reached.</summary>
    internal const string RunNotStartedYet = "It starts when the steps before it finish.";

    /// <summary>The heading of the failure block.</summary>
    internal const string RunWentWrong = "What went wrong";

    /// <summary>The heading of the block of a step that ended without failing.</summary>
    internal const string RunHowItEnded = "How it ended";

    /// <summary>The block's line when nothing more is recorded.</summary>
    internal const string RunNoDetail = "No more detail was recorded.";

    /// <summary>A refused report: a check turned it down.</summary>
    internal const string RunRefusedByCheck = "A check turned its report down.";

    /// <summary>The heading of the next actions.</summary>
    internal const string RunNext = "What you can do next";

    /// <summary>Take over's title.</summary>
    internal const string RunTakeOverTitle = "Take over and fix it yourself";

    /// <summary>Take over's help.</summary>
    internal const string RunTakeOverHelp = "Make the change, then hand it back. The clock pauses while you have it.";

    /// <summary>See its context's title.</summary>
    internal const string RunSeeContext = "See its context";

    /// <summary>See its context's help.</summary>
    internal const string RunSeeContextHelp = "The feature, screens and files this change touches.";

    /// <summary>The button that takes the run over.</summary>
    internal const string RunTakeOver = "Take over";

    /// <summary>The button that hands a taken-over run back.</summary>
    internal const string RunHandBack = "Hand back";

    /// <summary>The button that opens the decision in Needs you.</summary>
    internal const string RunDecide = "Decide in Needs you";

    /// <summary>The disclosure of the run's raw facts.</summary>
    internal const string RunTechnical = "Technical details";

    /// <summary>The disclosure of the step's raw status text.</summary>
    internal const string RunRawOutput = "Raw output";

    /// <summary>The inspector's close button.</summary>
    internal const string RunInspectorClose = "Close (Esc)";

    /// <summary>The inspector's previous button.</summary>
    internal const string RunInspectorPrevious = "Previous step (K)";

    /// <summary>The inspector's next button.</summary>
    internal const string RunInspectorNext = "Next step (J)";

    /// <summary>Who ran a step, when an agent did.</summary>
    internal const string KindAgent = "AI agent";

    /// <summary>Who ran a step, when you did.</summary>
    internal const string KindYou = "You";

    /// <summary>The label of the lock button on the approve step.</summary>
    internal const string RunApprove = "Approve";

    /// <summary>A run that is going.</summary>
    /// <param name="span">How long it has gone.</param>
    /// <returns>"Running for 4m".</returns>
    internal static string RunRunningFor(string span) => $"Running for {span}";

    /// <summary>A run that ended.</summary>
    /// <param name="span">How long it took.</param>
    /// <returns>"Took 4 min".</returns>
    internal static string RunTook(string span) => $"Took {span}";

    /// <summary>The tokens a run spent.</summary>
    /// <param name="tokens">The count.</param>
    /// <returns>"61k used".</returns>
    internal static string RunTokensUsed(int tokens) => $"{Tokens(tokens)} used";

    /// <summary>A timeline row's accessible name.</summary>
    /// <param name="attempt">The try.</param>
    /// <param name="word">The state's word.</param>
    /// <param name="times">When and for how long.</param>
    /// <returns>The name.</returns>
    internal static string RunTimelineName(string attempt, string word, string times) => $"{attempt}, {word}, {times}";
}
