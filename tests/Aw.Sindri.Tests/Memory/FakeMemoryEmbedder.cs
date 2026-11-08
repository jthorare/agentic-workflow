using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Tests.Memory;

/// <summary>An <see cref="IMemoryEmbedder"/> with a fixed vector per text, or none at all (the model is off).</summary>
internal sealed class FakeMemoryEmbedder : IMemoryEmbedder
{
    private readonly Dictionary<string, float[]> _vectors = new(StringComparer.Ordinal);

    /// <summary>Gets or sets a value indicating whether the model is off: every call answers Unavailable.</summary>
    public bool Off { get; set; }

    /// <summary>Gets the texts embedded so far, in order.</summary>
    public List<string> Seen { get; } = [];

    /// <summary>Fixes the vector a text embeds to.</summary>
    /// <param name="text">The text.</param>
    /// <param name="vector">Its vector.</param>
    /// <returns>This embedder.</returns>
    public FakeMemoryEmbedder With(string text, params float[] vector)
    {
        _vectors[text] = vector;
        return this;
    }

    public ValueTask<MemoryEmbedding> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        Seen.AddRange(texts);
        if (Off)
        {
            return ValueTask.FromResult(MemoryEmbedding.Unavailable("the fake model is off"));
        }

        return ValueTask.FromResult(MemoryEmbedding.Of([.. texts.Select(t => new ReadOnlyMemory<float>(_vectors.TryGetValue(t, out var v) ? v : [0f, 0f, 0f, 1f]))]));
    }
}
