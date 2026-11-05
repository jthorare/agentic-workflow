using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IGateStore: the one fake, kept honest by GateStoreContract.</summary>
public sealed class InMemoryGateStore : IGateStore
{
    private readonly List<GateRecord> _gates = [];
    private readonly List<MergeRecord> _merges = [];

    /// <inheritdoc />
    public ValueTask<Result<GateRecord>> Save(GateRecord gate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gate);
        var stored = gate with { Run = Math.Max(_gates.Where(g => g.Pr == gate.Pr).Select(g => g.Run).DefaultIfEmpty(0).Max() + 1, gate.Run) };
        _gates.Add(stored);
        return Done.Of(stored);
    }

    /// <inheritdoc />
    public ValueTask<Result<GateRecord?>> Latest(int pr, CancellationToken cancellationToken) =>
        Done.Of(_gates.Where(g => g.Pr == pr).MaxBy(g => g.Run));

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<GateRecord>>> History(int pr, CancellationToken cancellationToken) =>
        Done.Of<IReadOnlyList<GateRecord>>([.. _gates.Where(g => g.Pr == pr).OrderByDescending(g => g.Run)]);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Classify(int pr, int run, string step, TestVerdict verdict, CancellationToken cancellationToken)
    {
        var at = _gates.FindIndex(g => g.Pr == pr && g.Run == run);
        var steps = at < 0 ? null : _gates[at].Steps.ToList();
        var index = steps?.FindIndex(s => s.Name == step && s.Verdict != GateVerdict.Pass) ?? -1;
        if (steps is null || index < 0)
        {
            return Done.Failed<Unit>(MetaErrors.GateStepNotFound.With(string.Create(CultureInfo.InvariantCulture, $"no failed step {step} in gate {pr}/{run}")));
        }

        steps[index] = steps[index] with { Reruns = [.. steps[index].Reruns.Where(r => r.Test != verdict.Test), verdict] };
        _gates[at] = _gates[at] with { Steps = steps };
        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> RecordMerge(MergeRecord merge, CancellationToken cancellationToken)
    {
        _merges.Add(merge);
        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<MergeRecord?>> FindMerge(int pr, CancellationToken cancellationToken) =>
        Done.Of(_merges.Where(m => m.Pr == pr).MaxBy(m => m.At));

    /// <inheritdoc />
    public ValueTask<Result<MergeRecord?>> LatestMerge(CancellationToken cancellationToken) =>
        Done.Of(_merges.MaxBy(m => m.At));
}
