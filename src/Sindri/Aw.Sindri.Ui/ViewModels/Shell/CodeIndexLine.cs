namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One line of the card: the words, and the accessible name that leads with the card's name.</summary>
/// <param name="Text">"repo @ abcdef0, 3 min old, structure ok · graph ok · embeddings pending 31%".</param>
internal sealed record CodeIndexLine(string Text)
{
    /// <summary>The command that fixes the line, on a row of its own; empty when the line has none.</summary>
    public string Command => Text[..0];   // red stub (#3576)

    /// <summary>The line read aloud: "Code index: " first.</summary>
    public string Name { get; } = Labels.CodeIndexNamePrefix + Text;
}
