using Aw.Context.Core.Surfaces;
using Aw.Kernel;

namespace Aw.Context.Testing.Surfaces;

/// <summary>An in-memory <see cref="ISurfacePickStore"/>; <see cref="Failure"/> makes every call fail.</summary>
public sealed class FakeSurfacePickStore : ISurfacePickStore
{
    private readonly Dictionary<string, SurfaceDecision> _decisions = new(StringComparer.Ordinal);

    /// <summary>When set, every call fails with it.</summary>
    public Error? Failure { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, SurfaceDecision>>> Get(IReadOnlyCollection<string> groupKeys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groupKeys);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<IReadOnlyDictionary<string, SurfaceDecision>>>(failure);
        }

        IReadOnlyDictionary<string, SurfaceDecision> found = groupKeys.Where(_decisions.ContainsKey).Distinct(StringComparer.Ordinal).ToDictionary(k => k, k => _decisions[k], StringComparer.Ordinal);
        return ValueTask.FromResult(Results.Success(found));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Save(IReadOnlyList<SurfaceDecision> decisions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<Unit>>(failure);
        }

        foreach (var decision in decisions)
        {
            _decisions[decision.GroupKey] = decision;
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
