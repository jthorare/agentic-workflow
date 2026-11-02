namespace Aw.Embeddings;

/// <summary>The error body Ollama sends with a 4xx.</summary>
/// <param name="Error">The message.</param>
internal sealed record ErrorResponse(string? Error);
