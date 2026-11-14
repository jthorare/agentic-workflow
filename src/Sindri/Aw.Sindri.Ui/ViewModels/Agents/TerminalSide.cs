namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The side of the terminal view that has keyboard focus (agents spec §15.3).</summary>
internal enum TerminalSide
{
    /// <summary>The 360 px list on the left.</summary>
    List,

    /// <summary>The live pane on the right: every key but the focus chords goes to the agent.</summary>
    Pane,

    /// <summary>The first (or Tab-selected) button of a card or banner: states with nothing to type into never give focus to a pane.</summary>
    Card,
}
