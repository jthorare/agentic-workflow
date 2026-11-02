using Xunit;

namespace Aw.Embeddings.Testing;

/// <summary>The <see cref="IMatrixScan"/> contract: the same cases for the in-process scan and any scan that replaces it behind the port.</summary>
public abstract class VectorScanContract
{
    /// <summary>Gets the implementation.</summary>
    /// <returns>The scan.</returns>
    protected abstract IMatrixScan Arrange();

    /// <summary>The nearest row comes first, then the rest by falling score.</summary>
    [Fact]
    public void Hits_come_best_score_first()
    {
        var hits = Arrange().Top(Matrix([1, 0], [0.6f, 0.8f], [0, 1], [-1, 0]), new float[] { 1, 0 }, default, 3);

        Expect.That(hits.IsSuccess, "a well-sized scan succeeds");
        Expect.That(hits.Value.Select(h => h.Row).SequenceEqual([0, 1, 2]), "rows 0, 1, 2 by falling score");
        Expect.That(Math.Abs(hits.Value[1].Score - 0.6f) < 1e-5f, "the score is the dot product");
    }

    /// <summary>Equal scores keep row order.</summary>
    [Fact]
    public void Ties_keep_row_order()
    {
        var hits = Arrange().Top(Matrix([1, 0], [1, 0], [1, 0]), new float[] { 1, 0 }, default, 2);

        Expect.That(hits.Value.Select(h => h.Row).SequenceEqual([0, 1]), "the earlier rows win a tie");
    }

    /// <summary>A count above the row count returns every row.</summary>
    [Fact]
    public void A_count_above_the_row_count_returns_every_row()
    {
        var hits = Arrange().Top(Matrix([1, 0], [0, 1]), new float[] { 1, 0 }, default, 1000);

        Expect.That(hits.Value.Count == 2, "both rows come back");
    }

    /// <summary>An empty matrix has no hits.</summary>
    [Fact]
    public void An_empty_matrix_has_no_hits()
    {
        var hits = Arrange().Top(ReadOnlyMemory<float>.Empty, new float[] { 1, 0 }, default, 5);

        Expect.That(hits.IsSuccess && hits.Value.Count == 0, "no rows, no hits, no error");
    }

    /// <summary>A masked-out row never appears, even when it is the best one: the mask applies before the top rows are taken.</summary>
    [Fact]
    public void The_mask_applies_before_the_top_rows_are_taken()
    {
        var hits = Arrange().Top(Matrix([1, 0], [0.6f, 0.8f], [0, 1]), new float[] { 1, 0 }, _maskSecondAndThird, 1);

        Expect.That(hits.Value.Count == 1 && hits.Value[0].Row == 1, "the best allowed row is returned");
    }

    /// <summary>Wrong sizes are AW-EMBEDDINGS-002.</summary>
    [Fact]
    public void A_wrong_size_is_refused()
    {
        var scan = Arrange();
        var matrix = Matrix([1, 0], [0, 1]);

        Expect.That(scan.Top(matrix, new float[] { 1, 0, 0 }, default, 1).IsError, "a query that does not divide the matrix");
        Expect.That(scan.Top(matrix, ReadOnlyMemory<float>.Empty, default, 1).IsError, "an empty query");
        Expect.That(scan.Top(matrix, new float[] { 1, 0 }, _maskOfOne, 1).IsError, "a mask with the wrong number of flags");
        Expect.That(scan.Top(matrix, new float[] { 1, 0 }, default, 0).IsError, "a count of zero");
        Expect.That(scan.Top(matrix, new float[] { 1, 0 }, default, 1001).IsError, "a count above 1,000");
        Expect.That(scan.Top(matrix, new float[] { 1, 0 }, default, 1).IsSuccess, "the same call, sized right");
    }

    private static readonly bool[] _maskSecondAndThird = [false, true, true];
    private static readonly bool[] _maskOfOne = [true];

    private static float[] Matrix(params float[][] rows) => [.. rows.SelectMany(r => r)];
}
