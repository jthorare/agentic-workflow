namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One line of the Context index card: the words, and the accessible name that leads with the card's name.</summary>
/// <param name="Text">"linear: idle · 0 records · nothing committed yet".</param>
internal sealed record ContextIndexLine(string Text)
{
    /// <summary>The line read aloud: "Context index: " first.</summary>
    public string Name { get; } = Labels.ContextIndexNamePrefix + Text;

    /// <summary>The words up to the first ": ", colon kept ("linear:"); empty when the line has none (#4269, not built yet).</summary>
    public string Label { get; } = string.Empty;

    /// <summary>The words after the label, with codes and time phrases glued so a wrap can't split them (#4269, not built yet).</summary>
    public string Rest { get; } = string.Empty;

    /// <summary>True when the line has a label column.</summary>
    public bool HasLabel => Label.Length > 0;
}
