using System.Numerics;
using Aw.Kernel;

namespace Aw.Embeddings;

/// <summary>A brute-force scan in this process: one dot product per row over <see cref="Vector{T}"/>, a sorted top-k kept as it goes.</summary>
public sealed class ScanInProcess : IMatrixScan
{
    /// <inheritdoc />
    public Result<IReadOnlyList<ScanHit>> Top(ReadOnlyMemory<float> matrix, ReadOnlyMemory<float> query, ReadOnlyMemory<bool> mask, int count)
    {
        var dimension = query.Length;
        if (dimension == 0 || count < 1 || count > IMatrixScan.MaxCount || matrix.Length % dimension != 0)
        {
            return EmbeddingsErrors.VectorInvalid.With("the scan's query, matrix or count has the wrong size");
        }

        var rows = matrix.Length / dimension;
        if (mask.Length != 0 && mask.Length != rows)
        {
            return EmbeddingsErrors.VectorInvalid.With("the scan's mask does not have one flag per row");
        }

        var best = new BestRows(Math.Min(count, rows));
        var data = matrix.Span;
        var flags = mask.Span;
        for (var row = 0; row < rows; row++)
        {
            if (flags.Length == 0 || flags[row])
            {
                best.Offer(row, Dot(data.Slice(row * dimension, dimension), query.Span));
            }
        }

        return best.ToHits();
    }

    private static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var width = Vector<float>.Count;
        var sum = Vector<float>.Zero;
        var i = 0;
        for (; i <= a.Length - width; i += width)
        {
            sum += new Vector<float>(a[i..]) * new Vector<float>(b[i..]);
        }

        var total = Vector.Sum(sum);
        for (; i < a.Length; i++)
        {
            total += a[i] * b[i];
        }

        return total;
    }

    /// <summary>The best rows so far, kept sorted; strictly-greater insertion keeps an earlier row ahead of a later one with the same score.</summary>
    private sealed class BestRows(int capacity)
    {
        private readonly (int Row, float Score)[] _best = new (int, float)[capacity];
        private int _kept;

        public void Offer(int row, float score)
        {
            if (float.IsNaN(score) || (_kept == _best.Length && !(score > _best[_kept - 1].Score)))
            {
                return;
            }

            var at = _kept == _best.Length ? _kept - 1 : _kept++;
            while (at > 0 && _best[at - 1].Score < score)
            {
                _best[at] = _best[at - 1];
                at--;
            }

            _best[at] = (row, score);
        }

        public ScanHit[] ToHits() => [.. _best.Take(_kept).Select(b => new ScanHit(b.Row, b.Score))];
    }
}
