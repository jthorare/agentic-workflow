using Aw.Context.Core.ConceptScan;
using Aw.Kernel;

namespace Aw.Context.Testing.Map;

/// <summary>An in-memory <see cref="IConceptHintStore"/>; <see cref="ConceptHintStoreContract"/> holds it to the SQLite store's behavior.</summary>
public sealed class FakeConceptHintStore : IConceptHintStore
{
    private readonly Dictionary<int, IReadOnlyList<ConceptHint>> _byVersion = [];

    /// <summary>How many times a scan saved hints.</summary>
    public int Saves { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Save(int version, IReadOnlyList<ConceptHint> hints, DateTimeOffset scanned, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hints);
        _byVersion[version] = [.. hints];
        Saves++;
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ConceptHint>>> Read(int version, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(_byVersion.GetValueOrDefault(version) ?? []));
}
