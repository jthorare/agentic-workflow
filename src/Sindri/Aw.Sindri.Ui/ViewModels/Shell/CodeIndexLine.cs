namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One line of the card: the words, an optional command on a row of its own, and the accessible name that leads with the card's name.</summary>
/// <param name="Text">"repo @ abcdef0, 3 min old, structure ok · graph ok · embeddings pending 31%".</param>
/// <param name="Command">The command that fixes the line ("sindri services resume code-index-main-move"); empty when the line has none.</param>
internal sealed record CodeIndexLine(string Text, string Command = "")
{
    /// <summary>True when the line has a command row.</summary>
    public bool HasCommand => Command.Length > 0;

    /// <summary>The line read aloud: "Code index: " first, then the words and the command.</summary>
    public string Name { get; } = Labels.CodeIndexNamePrefix + Text;
}
