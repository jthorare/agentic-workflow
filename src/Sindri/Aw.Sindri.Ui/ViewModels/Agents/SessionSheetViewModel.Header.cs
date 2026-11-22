namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The session sheet's header (<see cref="SessionSheetViewModel"/>, continued; #3871, #3872): the state's dot and its tone, and the attach command shown before it is copied.</summary>
internal sealed partial class SessionSheetViewModel
{
    /// <summary>The state's dot ("●"); empty before a session is drawn.</summary>
    public string StateGlyph { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the session is working: the dot is green.</summary>
    public bool IsStateWorking { get; private set => Set(ref field, value); }

    /// <summary>True when the session waits on the person: the dot is amber.</summary>
    public bool IsStateWaiting { get; private set => Set(ref field, value); }

    /// <summary>The attach command as Copy attach command copies it, shown under the chips; empty when the session is not in tmux.</summary>
    public string AttachText { get; private set => Set(ref field, value); } = string.Empty;
}
