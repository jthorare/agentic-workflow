namespace Aw.Embeddings;

/// <summary>The answer of GET /api/tags.</summary>
/// <param name="Models">The installed models.</param>
internal sealed record TagsResponse(IReadOnlyList<InstalledModel>? Models);
