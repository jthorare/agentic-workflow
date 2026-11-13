namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One line of the card: the words, and the accessible name that leads with the card's name.</summary>
/// <param name="Text">"repo @ abcdef0, 3 min old, structure ok · graph ok · embeddings pending 31%".</param>
internal sealed record CodeIndexLine(string Text)
{
    /// <summary>The line read aloud: "Code index: " first, then the words.</summary>
    public string Name { get; } = Labels.CodeIndexNamePrefix + Text;
}
