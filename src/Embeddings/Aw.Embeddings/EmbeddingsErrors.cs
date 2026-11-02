using Aw.Kernel;

namespace Aw.Embeddings;

/// <summary>The Embeddings error catalogue (codes AW-EMBEDDINGS-NNN).</summary>
public static class EmbeddingsErrors
{
    /// <summary>AW-EMBEDDINGS-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-EMBEDDINGS-001", ErrorKind.Invalid, "Embeddings input failed validation");

    /// <summary>AW-EMBEDDINGS-002.</summary>
    public static readonly ErrorDefinition VectorInvalid = new("AW-EMBEDDINGS-002", ErrorKind.Invalid, "A vector blob, query, mask or count has the wrong size");

    /// <summary>AW-EMBEDDINGS-003.</summary>
    public static readonly ErrorDefinition TargetRefused = new("AW-EMBEDDINGS-003", ErrorKind.Invalid, "The embedding URL is not loopback, or the model is a cloud model");

    /// <summary>AW-EMBEDDINGS-004.</summary>
    public static readonly ErrorDefinition ServerFailed = new("AW-EMBEDDINGS-004", ErrorKind.Unavailable, "The embedding server failed");

    /// <summary>AW-EMBEDDINGS-005.</summary>
    public static readonly ErrorDefinition PinMismatch = new("AW-EMBEDDINGS-005", ErrorKind.Unavailable, "The embedding model is missing or is not the pinned digest");

    /// <summary>Every Embeddings error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid, VectorInvalid, TargetRefused, ServerFailed, PinMismatch];
}
