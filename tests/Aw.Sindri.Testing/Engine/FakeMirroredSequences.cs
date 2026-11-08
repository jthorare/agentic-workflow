using Aw.Kernel;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Engine;

/// <summary>The ledger rows that mirror Temporal events, by activity id: what a test says the ledger holds.</summary>
public sealed class FakeMirroredSequences : IMirroredSequences
{
    private readonly Dictionary<string, long> _rows = new(StringComparer.Ordinal);

    /// <summary>The ledger sequence of the row whose temporal event id is the key.</summary>
    public IDictionary<string, long> Rows => _rows;

    /// <summary>The (address, run) pairs it was asked about.</summary>
    public IList<(InstanceAddress Address, string RunId)> Asked { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, long>>> Of(InstanceAddress address, string run, IReadOnlyCollection<string> activities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activities);
        Asked.Add((address, run));
        IReadOnlyDictionary<string, long> found = activities.Where(_rows.ContainsKey).Distinct(StringComparer.Ordinal).ToDictionary(static id => id, id => _rows[id], StringComparer.Ordinal);
        return ValueTask.FromResult(Results.Success(found));
    }
}
