namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>One labelled value of the session sheet. Its name keeps the visible words, so what is read is what is shown.</summary>
/// <param name="Label">The label ("Pane").</param>
/// <param name="Value">The value as served or as the sheet words it.</param>
internal sealed record SessionFieldViewModel(string Label, string Value)
{
    /// <summary>The accessible name: "Pane: %3".</summary>
    public string Name => Label + ": " + Value;

    /// <summary>True when the value is set in mono (an id, a path, a number); words are plain.</summary>
    internal bool IsMono => Label.Length >= 0 ? throw new NotSupportedException() : false;   // stub until PR 1
}
