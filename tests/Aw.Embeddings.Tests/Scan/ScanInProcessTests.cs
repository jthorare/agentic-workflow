namespace Aw.Embeddings.Tests.Scan;

[Trait("Lane", "unit")]
public sealed class ScanInProcessTests
{
    private static readonly bool[] _noRows = [false, false];
    private static readonly int[] _bestTwo = [2, 3];

    [Fact]
    public void A_wide_matrix_matches_a_scalar_scan()
    {
        // 37 columns: wider than a Vector<float> and not a multiple of it, so the tail loop runs.
        const int dimension = 37;
        const int rows = 200;
        var random = new Random(7);
        var matrix = new float[rows * dimension];
        var query = new float[dimension];
        for (var i = 0; i < matrix.Length; i++)
        {
            matrix[i] = (float)(random.NextDouble() - 0.5);
        }

        for (var i = 0; i < dimension; i++)
        {
            query[i] = (float)(random.NextDouble() - 0.5);
        }

        var expected = Enumerable.Range(0, rows)
            .Select(r => (Row: r, Score: Enumerable.Range(0, dimension).Sum(c => (double)matrix[(r * dimension) + c] * query[c])))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Row).Take(10).Select(x => x.Row).ToArray();

        var hits = new ScanInProcess().Top(matrix, query, default, 10);

        int[] rowsHit = [.. hits.Value.Select(h => h.Row)];
        Assert.Equal(expected, rowsHit);
    }

    [Fact]
    public void A_mask_that_excludes_every_row_has_no_hits()
    {
        var hits = new ScanInProcess().Top(new float[] { 1, 0, 0, 1 }, new float[] { 1, 0 }, _noRows, 5);

        Assert.Empty(hits.Value);
    }

    [Fact]
    public void A_count_below_the_row_count_keeps_only_the_best()
    {
        var hits = new ScanInProcess().Top(new float[] { 0, 1, 0.5f, 0.5f, 1, 0, 0.9f, 0.1f }, new float[] { 1, 0 }, default, 2);

        int[] rowsHit = [.. hits.Value.Select(h => h.Row)];
        Assert.Equal(_bestTwo, rowsHit);
    }
}
