namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One key hint in the Projects page's footer: the key, then what it does.</summary>
/// <param name="Key">The key as drawn ("j/k").</param>
/// <param name="Word">What it does ("move").</param>
internal sealed record ProjectKeyHint(string Key, string Word)
{
    /// <summary>The hint's accessible name: "j/k move".</summary>
    public string Name => Key + " " + Word;
}
