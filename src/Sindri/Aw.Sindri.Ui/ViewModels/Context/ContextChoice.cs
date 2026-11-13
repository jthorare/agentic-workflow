namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One "Did you mean" choice (#2809): the title a person reads, the node's kind as a small tag, and the key a pick reads.</summary>
/// <param name="Key">The node key; <see cref="ContextPageViewModel.Pick"/> reads it.</param>
/// <param name="Title">The node's label, as the map's cards show it; the key when the server sent no label.</param>
/// <param name="Kind">The node's kind ('feature', 'concept', ...), or empty when unknown.</param>
internal sealed record ContextChoice(string Key, string Title, string Kind)
{
    /// <summary>Whether the kind tag shows.</summary>
    public bool HasKind => Kind.Length > 0;

    /// <summary>What a screen reader says: the title, then the kind.</summary>
    public string Name => HasKind ? $"{Title}, {Kind}" : Title;
}
