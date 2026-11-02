namespace Aw.Embeddings;

/// <summary>The body of POST /api/embed.</summary>
/// <param name="Model">The model name.</param>
/// <param name="Input">The texts.</param>
/// <param name="Truncate">Always false: an over-long input is an error, never a silent cut.</param>
internal sealed record EmbedRequest(string Model, IReadOnlyList<string> Input, bool Truncate);
