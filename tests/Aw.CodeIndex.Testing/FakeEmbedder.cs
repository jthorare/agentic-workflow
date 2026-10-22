using System.Security.Cryptography;
using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// An embedder with no server (spec §11): a text's vector is a fixed function of its SHA-256, so the same text always embeds to
/// the same vector and a different text to a different one. Applies the loopback and cloud-model refusal the contract asks of
/// every embedder. Tests script a failure with <see cref="FailProbe"/> and <see cref="FailEmbedAfter"/> and read <see cref="Batches"/>.
/// </summary>
public sealed class FakeEmbedder : IEmbedder
{
    /// <summary>The model the fake reports when the settings name none.</summary>
    public const string DefaultModel = "fake-embed:1";

    /// <summary>The dimension of every vector.</summary>
    public const int Dimensions = 16;

    private readonly List<IReadOnlyList<string>> _batches = [];
    private int _succeeded;

    /// <summary>Every batch embedded, in order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Batches => _batches;

    /// <summary>How many times <see cref="Probe"/> was called.</summary>
    public int Probes { get; private set; }

    /// <summary>Makes <see cref="Probe"/> fail with this error, or null for success.</summary>
    public Error? FailProbe { get; set; }

    /// <summary>Makes <see cref="Embed"/> fail with this error once that many batches have succeeded, or never when null.</summary>
    public (int AfterBatches, Error Error)? FailEmbedAfter { get; set; }

    /// <summary>The vector a text embeds to.</summary>
    /// <param name="text">The text.</param>
    /// <returns>A vector of <see cref="Dimensions"/> numbers in [-1, 1), never all zero.</returns>
    public static float[] VectorOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var vector = new float[Dimensions];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (hash[i] / 128f) - 1f + 0.001f;
        }

        return vector;
    }

    /// <inheritdoc />
    public string ModelFor(EmbeddingsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Model ?? DefaultModel;
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Probe(EmbeddingsSettings settings, CancellationToken cancellationToken)
    {
        Probes++;
        var error = EmbeddingTarget.Check(settings) ?? FailProbe;
        return ValueTask.FromResult(error is null ? Results.Success(Unit.Value) : Results.Failure<Unit>(error));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> Embed(EmbeddingsSettings settings, IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var error = EmbeddingTarget.Check(settings);
        if (error is null && FailEmbedAfter is { } failure && _succeeded >= failure.AfterBatches)
        {
            error = failure.Error;
        }

        if (error is not null)
        {
            return ValueTask.FromResult(Results.Failure<IReadOnlyList<ReadOnlyMemory<float>>>(error));
        }

        _succeeded++;
        _batches.Add([.. texts]);
        return ValueTask.FromResult(Results.Success<IReadOnlyList<ReadOnlyMemory<float>>>([.. texts.Select(t => (ReadOnlyMemory<float>)VectorOf(t))]));
    }
}
