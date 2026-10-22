namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the user can answer a paused run (F3 needs-you spec §5).</summary>
internal enum PausedAnswerOption
{
    /// <summary>Allow more tries: protected, so the button is the terminal's with the stepper's rounds filled in.</summary>
    MoreTries = 1,

    /// <summary>Give a hint: ApplyCommand:answer-text with the typed text.</summary>
    Hint = 2,

    /// <summary>Stop this run: ApplyCommand:answer-reject.</summary>
    Stop = 3,
}
