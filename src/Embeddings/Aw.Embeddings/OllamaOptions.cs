namespace Aw.Embeddings;

/// <summary>Where the embedder talks and which model it must find there.</summary>
/// <param name="Url">The server, an http URL on 127.0.0.1 or [::1] ('localhost' is refused).</param>
/// <param name="Model">The model name as Ollama lists it ('nomic-embed-text:latest'); never a cloud model.</param>
/// <param name="Digest">The pinned manifest digest, 'sha256:' and 64 hex digits (the prefix is optional).</param>
public sealed record OllamaOptions(string Url, string Model, string Digest);
