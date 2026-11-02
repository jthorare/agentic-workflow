namespace Aw.Embeddings.Testing;

/// <summary>One request the fake saw.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path.</param>
/// <param name="Host">The host the request named.</param>
/// <param name="Model">The model of an embed request.</param>
/// <param name="Truncate">The embed request's truncate flag, or null when absent.</param>
/// <param name="Inputs">The embed request's inputs.</param>
public sealed record RecordedCall(string Method, string Path, string Host, string? Model, bool? Truncate, IReadOnlyList<string> Inputs);
