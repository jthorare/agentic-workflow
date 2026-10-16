namespace Aw.Evidence.Core.Data;

/// <summary>Checks a query's expectation against the rows it returned.</summary>
public static class DbExpectationCheck
{
    /// <summary>Runs every assertion (the row count, each pointer over the rows read as a JSON array, the golden); none stops the others.</summary>
    /// <param name="step">The query's 1-based index.</param>
    /// <param name="expect">The expectation.</param>
    /// <param name="rowCount">How many rows were kept.</param>
    /// <param name="rowsJson">The rows as a JSON array of objects.</param>
    /// <param name="goldenText">The golden file's text, when the expectation names one.</param>
    /// <returns>How many assertions there were and which failed (values uncapped and unscrubbed).</returns>
    public static CheckedExpectation Evaluate(int step, DbExpectation expect, int rowCount, string rowsJson, string? goldenText)
    {
        var inner = ExpectationCheck.Evaluate(step, new Expectation(Pointers: expect.Pointers, Golden: expect.Golden), actualCode: 0, rowsJson, goldenText);
        if (expect.RowCount is not { } wanted)
        {
            return inner;
        }

        if (wanted == rowCount)
        {
            return new CheckedExpectation(inner.Count + 1, inner.Failures, inner.FirstFailedOrdinal is { } held ? held + 1 : null);
        }

        var failure = new ApiFailure(step, Location: null, "rowCount " + Text(wanted), "rowCount " + Text(rowCount));
        return new CheckedExpectation(inner.Count + 1, [failure, .. inner.Failures], FirstFailedOrdinal: 1);
    }

    private static string Text(int number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
