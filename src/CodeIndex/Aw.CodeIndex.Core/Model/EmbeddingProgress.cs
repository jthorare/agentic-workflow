namespace Aw.CodeIndex.Core.Model;

/// <summary>How far the embeddings layer is ('31 of 100 embedded').</summary>
/// <param name="Embedded">Symbols with a vector.</param>
/// <param name="Embeddable">Symbols that should have one (symbols with a stored text or a vector; class-like ones by name and signature).</param>
public sealed record EmbeddingProgress(int Embedded, int Embeddable)
{
    /// <summary>Of <see cref="Embedded"/>, the vectors already in the index when an embed run began (nought outside a run).</summary>
    public int Carried { get; init; }

    /// <summary>The vectors this run wrote (nought outside a run).</summary>
    public int Made { get; init; }

    /// <summary>Symbols the model refused even alone (#3128): each has a marker row (an empty vector), so it is no candidate again, it is not in <see cref="Embedded"/> and it still counts in <see cref="Embeddable"/>.</summary>
    public int Refused { get; init; }
}
