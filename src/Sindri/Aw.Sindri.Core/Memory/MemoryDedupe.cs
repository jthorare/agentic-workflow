namespace Aw.Sindri.Core.Memory;

/// <summary>The write-time rule: a fact very similar to an existing one with the same tags refreshes it instead of adding a row.</summary>
public static class MemoryDedupe
{
    /// <summary>The cosine similarity at and above which two facts with the same tags are one fact. Calibrated over tests/fixtures/memory/pairs.json.</summary>
    public const float Threshold = 0.9f;

    /// <summary>The cosine similarity of two vectors.</summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>A value from -1 to 1; 0 when either vector is empty or zero.</returns>
    public static float Similarity(ReadOnlyMemory<float> left, ReadOnlyMemory<float> right) => throw new NotSupportedException();

    /// <summary>Whether two tag lists are the same set (case-insensitive, order and repeats ignored).</summary>
    /// <param name="left">The first list.</param>
    /// <param name="right">The second list.</param>
    /// <returns>True when they hold the same tags.</returns>
    public static bool SameTags(IReadOnlyList<string> left, IReadOnlyList<string> right) => throw new NotSupportedException();

    /// <summary>Finds the existing fact a new one duplicates.</summary>
    /// <param name="vector">The new fact's vector.</param>
    /// <param name="tags">The new fact's tags.</param>
    /// <param name="existing">The unexpired facts of the memory.</param>
    /// <returns>The most similar fact with the same tags at or above <see cref="Threshold"/>, or null.</returns>
    public static MemoryFact? FindDuplicate(ReadOnlyMemory<float> vector, IReadOnlyList<string> tags, IReadOnlyList<MemoryFact> existing) => throw new NotSupportedException();
}
