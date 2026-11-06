namespace Aw.Context.Core.Library;

/// <summary>The design-doc claims a PR body makes with its <c language="text">Covers:</c> line (spec 2026-11-04 §4.4).</summary>
/// <param name="Refs">The refs as written (backticks dropped), each once, in order; empty for <c language="text">Covers: none (...)</c>, for no line and for a doubled line.</param>
/// <param name="Declined">True for <c language="text">Covers: none (...)</c>: the PR claims no design doc.</param>
/// <param name="Doubled">True when the body has a second <c language="text">Covers:</c> line (SND-LIB-021): nothing is claimed.</param>
public sealed record PrCovers(IReadOnlyList<string> Refs, bool Declined, bool Doubled)
{
    /// <summary>A body with no Covers line.</summary>
    public static PrCovers None { get; } = new([], Declined: false, Doubled: false);

    /// <summary>Reads the claims out of a PR body.</summary>
    /// <param name="body">The PR body (cleaned text).</param>
    /// <returns>The claims; never null.</returns>
    public static PrCovers Parse(string? body) => throw new NotSupportedException();
}
