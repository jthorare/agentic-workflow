namespace Aw.Sindri.Core.Memory;

/// <summary>The answer of <see cref="IMemoryEmbedder"/>: vectors or the reason there are none.</summary>
/// <param name="Vectors">One vector per text, or null when unavailable.</param>
/// <param name="Reason">Why no model answered (informational, SND-MEM-004), or null.</param>
/// <param name="Model">The model (name and digest) that made the vectors, or null when unavailable or unknown.</param>
public sealed record MemoryEmbedding(IReadOnlyList<ReadOnlyMemory<float>>? Vectors, string? Reason, string? Model = null)
{
    /// <summary>Gets a value indicating whether vectors came back.</summary>
    public bool IsAvailable => Vectors is not null;

    /// <summary>Wraps vectors.</summary>
    /// <param name="vectors">The vectors.</param>
    /// <param name="model">The model that made them, or null.</param>
    /// <returns>The answer.</returns>
    public static MemoryEmbedding Of(IReadOnlyList<ReadOnlyMemory<float>> vectors, string? model = null) => new(Vectors: vectors, Reason: null, Model: model);

    /// <summary>Says no model answered.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>The answer.</returns>
    public static MemoryEmbedding Unavailable(string reason) => new(Vectors: null, Reason: reason);
}
