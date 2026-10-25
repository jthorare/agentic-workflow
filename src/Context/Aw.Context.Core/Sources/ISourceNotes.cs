namespace Aw.Context.Core.Sources;

/// <summary>
/// A source that has something to say about a search besides its hits: documents it found but did not return (hidden, not identifiable, or
/// over the limit). The engine reads the notes after the search and shows each as a degraded entry, so a drop is never silent.
/// </summary>
public interface ISourceNotes
{
    /// <summary>The notes the last search left, once: reading them clears them.</summary>
    /// <returns>One sentence per kind of drop, with the count and the fix; empty when nothing was dropped.</returns>
    public IReadOnlyList<string> TakeNotes();
}
