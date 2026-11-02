using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Context.Testing.Records;
using Xunit;

namespace Aw.Context.Testing.Vectors;

/// <summary>The contract every <see cref="IVectorScan"/> honours (spec §4.4): best score first with ties in row order, the mask applied before the count, and a request that doesn't fit the matrix refused.</summary>
/// <typeparam name="TScan">The implementation.</typeparam>
public abstract class VectorScanContract<TScan>
    where TScan : IVectorScan
{
    private static readonly string _owner = typeof(TScan).Name;

    private static readonly float[] _unit = [1, 0];
    private static readonly float[] _wide = [1, 0, 0];
    private static readonly bool[] _middle = [false, true, true, false];
    private static readonly bool[] _none = [false, false, false, false];
    private static readonly bool[] _short = [true];

    private static VectorMatrix Matrix { get; } = new(2, new float[] { 1, 0, 0, 1, 0.6f, 0.8f, 1, 0 }, ["c0", "c1", "c2", "c3"]);

    /// <summary>A fresh scan.</summary>
    /// <returns>The scan.</returns>
    protected abstract TScan Create();

    /// <summary>Hits come best first, ties in row order, capped by the count.</summary>
    [Fact]
    public void Hits_come_best_first_with_ties_in_row_order_and_capped()
    {
        var scan = Create();
        var hits = ContractCheck.Ok(_owner, scan.Top(Matrix, _unit, ReadOnlyMemory<bool>.Empty, 3));
        ContractCheck.That(_owner, hits.Select(h => h.Row).SequenceEqual([0, 3, 2]), "rows 0 and 3 tie at 1, then row 2");
        ContractCheck.That(_owner, hits[0].Key == "c0" && MathF.Abs(hits[0].Score - 1f) < 1e-5f && MathF.Abs(hits[2].Score - 0.6f) < 1e-5f, "keys and cosine scores");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, scan.Top(Matrix, _unit, ReadOnlyMemory<bool>.Empty, 1)).Count == 1, "the count caps the answer");
    }

    /// <summary>The mask is applied before the top rows are taken.</summary>
    [Fact]
    public void The_mask_applies_before_the_count()
    {
        var scan = Create();
        var hits = ContractCheck.Ok(_owner, scan.Top(Matrix, _unit, _middle, 1));
        ContractCheck.That(_owner, hits.Select(h => h.Row).SequenceEqual([2]), "the best allowed row is returned, not an empty answer");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, scan.Top(Matrix, _unit, _none, 5)).Count == 0, "a mask that allows nothing answers nothing");
    }

    /// <summary>A query, mask or count that doesn't fit is refused.</summary>
    [Fact]
    public void A_request_that_does_not_fit_is_refused()
    {
        var scan = Create();
        ContractCheck.Fails(_owner, scan.Top(Matrix, _wide, ReadOnlyMemory<bool>.Empty, 1), ContextErrors.VectorScanInvalid.Code);
        ContractCheck.Fails(_owner, scan.Top(Matrix, _unit, _short, 1), ContextErrors.VectorScanInvalid.Code);
        ContractCheck.Fails(_owner, scan.Top(Matrix, _unit, ReadOnlyMemory<bool>.Empty, 0), ContextErrors.VectorScanInvalid.Code);
    }
}
