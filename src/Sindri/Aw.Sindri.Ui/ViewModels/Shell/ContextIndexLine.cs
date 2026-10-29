namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One line of the Context index card: the words, and the accessible name that leads with the card's name.</summary>
/// <param name="Text">"linear: idle · 0 records · nothing committed yet".</param>
internal sealed record ContextIndexLine(string Text)
{
    /// <summary>The line read aloud: "Context index: " first.</summary>
    public string Name { get; } = Labels.ContextIndexNamePrefix + Text;
}
