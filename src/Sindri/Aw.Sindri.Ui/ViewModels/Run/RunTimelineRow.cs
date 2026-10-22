namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>One row of the inspector's step timeline: a try of the step, where it came to, when, and who ran it.</summary>
/// <param name="Token">The run's token, for the technical details.</param>
/// <param name="TryText">"Try 2 of 3".</param>
/// <param name="State">Where the try came to.</param>
/// <param name="Word">The state's word.</param>
/// <param name="Icon">The state's mark (an Icons.axaml name).</param>
/// <param name="TextBrush">The brush key of the glyph and the word.</param>
/// <param name="Note">The sentence under the row: why a refused run failed; empty for none.</param>
/// <param name="TimesText">"Running for 4m", "Took 4 min", or the no-times line.</param>
/// <param name="Who">"AI agent" or "You"; empty when not served.</param>
/// <param name="TokensText">"61k used"; empty when none was recorded.</param>
/// <param name="Raw">The run as served, one line, for the technical details.</param>
/// <param name="AutomationName">The row's accessible name.</param>
internal sealed record RunTimelineRow(
    string Token,
    string TryText,
    RunNodeState State,
    string Word,
    string Icon,
    string TextBrush,
    string Note,
    string TimesText,
    string Who,
    string TokensText,
    string Raw,
    string AutomationName)
{
    /// <summary>True when the row has a sentence under it.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>True when an actor is named.</summary>
    public bool HasWho => Who.Length > 0;

    /// <summary>True when tokens were recorded.</summary>
    public bool HasTokens => TokensText.Length > 0;

    /// <summary>True while the try is going (its mark spins).</summary>
    public bool IsRunning => State == RunNodeState.Running;
}
