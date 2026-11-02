using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Kernel;

namespace Aw.Context.Testing.Vectors;

/// <summary>An <see cref="IChunkEmbedder"/> over <see cref="FakeQueryEmbedder"/>'s bag-of-words vectors; it passes <see cref="ChunkEmbedderContract{TEmbedder}"/>.</summary>
public sealed class FakeChunkEmbedder : IChunkEmbedder
{
    private readonly FakeQueryEmbedder _inner = new();
    private readonly List<IReadOnlyList<string>> _batches = [];

    /// <summary>Every batch the embedder was asked, in order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Batches => _batches;

    /// <summary>When set, every call fails with it (the model is down, say).</summary>
    public Error? Failure { get => _inner.Failure; set => _inner.Failure = value; }

    /// <summary>When set, it runs after the vectors are made and before they are returned: the moment a chunk can change under a batch.</summary>
    public Action? OnEmbedded { get; set; }

    /// <inheritdoc />
    public string Model => FakeQueryEmbedder.Model;

    /// <inheritdoc />
    public async ValueTask<Result<ChunkEmbeddings>> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count is < 1 or > 32)
        {
            return ContextErrors.QueryInvalid.With("a batch embeds 1 to 32 texts");
        }

        _batches.Add(texts);
        var vectors = new List<ReadOnlyMemory<float>>(texts.Count);
        foreach (var text in texts)
        {
            var one = await _inner.Embed(text, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            vectors.Add(one.Value.Vec);
        }

        OnEmbedded?.Invoke();
        return new ChunkEmbeddings(Model, vectors);
    }
}
