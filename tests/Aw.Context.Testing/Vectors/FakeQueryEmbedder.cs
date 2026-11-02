using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Kernel;

namespace Aw.Context.Testing.Vectors;

/// <summary>An <see cref="IQueryEmbedder"/> that hashes words into a small bag-of-words vector, so texts that share words are close; it passes <see cref="QueryEmbedderContract{TEmbedder}"/>.</summary>
public sealed class FakeQueryEmbedder : IQueryEmbedder
{
    /// <summary>The dimension of every vector.</summary>
    public const int Dim = 16;

    /// <summary>The model name every vector carries.</summary>
    public const string Model = "fake-embedder@1";

    /// <summary>When set, every call fails with it (the model is down, say).</summary>
    public Error? Failure { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<QueryEmbedding>> Embed(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<QueryEmbedding>>(failure);
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return ValueTask.FromResult<Result<QueryEmbedding>>(ContextErrors.QueryInvalid.With("the text to embed is empty"));
        }

        var vec = new float[Dim];
        foreach (var word in words)
        {
            vec[Bucket(word.ToUpperInvariant())]++;
        }

        var norm = MathF.Sqrt(vec.Sum(c => c * c));
        for (var i = 0; i < vec.Length; i++)
        {
            vec[i] /= norm;
        }

        return ValueTask.FromResult<Result<QueryEmbedding>>(new QueryEmbedding(Model, vec));
    }

    // A stable hash: string.GetHashCode is randomised per process, and a contract needs the same vector on every run.
    private static int Bucket(string word)
    {
        var hash = 17;
        foreach (var c in word)
        {
            hash = unchecked((hash * 31) + c);
        }

        return (int)((uint)hash % Dim);
    }
}
