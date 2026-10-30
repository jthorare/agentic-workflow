using Aw.Context.Core.Surfaces;
using Aw.Kernel;

namespace Aw.Context.Testing.Surfaces;

/// <summary>An in-memory <see cref="ISurfacePickStore"/>; <see cref="Failure"/> makes every call fail.</summary>
public sealed class FakeSurfacePickStore : ISurfacePickStore
{
    /// <summary>When set, every call fails with it.</summary>
    public Error? Failure { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, SurfaceDecision>>> Get(IReadOnlyCollection<string> groupKeys, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Save(IReadOnlyList<SurfaceDecision> decisions, CancellationToken cancellationToken) => throw new NotSupportedException();
}
