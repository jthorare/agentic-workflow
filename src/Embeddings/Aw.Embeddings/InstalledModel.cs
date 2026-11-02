namespace Aw.Embeddings;

/// <summary>One installed model.</summary>
/// <param name="Name">Its name and tag.</param>
/// <param name="Digest">Its digest, with or without the 'sha256:' prefix.</param>
internal sealed record InstalledModel(string? Name, string? Digest);
