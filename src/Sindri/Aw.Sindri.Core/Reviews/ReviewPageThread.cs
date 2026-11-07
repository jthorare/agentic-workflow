using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>One review thread.</summary>
/// <param name="Key">The forge's key.</param>
/// <param name="Path">The file; null on a top-level comment.</param>
/// <param name="Line">The line.</param>
/// <param name="Resolved">Whether it is resolved.</param>
/// <param name="Outdated">Whether the code changed.</param>
/// <param name="Comments">The comments.</param>
public sealed record ReviewPageThread(
    [property: JsonPropertyName("id")] string Key,
    string? Path,
    int? Line,
    bool Resolved,
    bool Outdated,
    IReadOnlyList<ReviewPageComment> Comments);
