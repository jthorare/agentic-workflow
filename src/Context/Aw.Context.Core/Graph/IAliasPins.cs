namespace Aw.Context.Core.Graph;

/// <summary>The owner's pin aliases as anchoring reads them (concept pins spec §3.4). A port in Graph, so matching never depends on the concepts that depend on it.</summary>
public interface IAliasPins
{
    /// <summary>The pin whose alias is exactly <paramref name="name"/> (compared as the matcher reads words).</summary>
    /// <param name="name">The name.</param>
    /// <returns>The pin's id, or null.</returns>
    public string? PinByAlias(string name);

    /// <summary>Every alias whose words all sit in <paramref name="words"/>, the longest first.</summary>
    /// <param name="words">The words of a question, as the matcher reads them.</param>
    /// <returns>Each match's pin id, the alias as the owner wrote it, and its word count.</returns>
    public IReadOnlyList<(string Pin, string Alias, int Words)> AliasesIn(IReadOnlySet<string> words);
}
