namespace Aw.CodeIndex.Infrastructure.Embedding;

/// <summary>The body of POST /api/embed.</summary>
/// <param name="Model">The model name.</param>
/// <param name="Input">The texts.</param>
/// <param name="Truncate">True: the server cuts a text past the model's context instead of answering 400.</param>
internal sealed record EmbedRequest(string Model, IReadOnlyList<string> Input, bool Truncate = true);
