namespace Aw.Sindri.Core.Setup;

/// <summary>What the ledger holds of 'setup.step.changed' per step, read once and kept current as events are appended, so a tick does not scan the ledger again.</summary>
public sealed class SetupStepLog
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SetupStepId, RecordedStep> _last = [];

    /// <summary>Gets a value indicating whether the ledger has been read into this log.</summary>
    public bool Loaded { get; private set; }

    /// <summary>A copy of the last recorded change of each step.</summary>
    /// <returns>The map.</returns>
    public IReadOnlyDictionary<SetupStepId, RecordedStep> Snapshot()
    {
        lock (_gate)
        {
            return new Dictionary<SetupStepId, RecordedStep>(_last);
        }
    }

    /// <summary>Counts one more change of <paramref name="id"/> and makes it the last.</summary>
    /// <param name="id">The step.</param>
    /// <param name="state">The state it moved to.</param>
    /// <param name="code">The error code, or null.</param>
    public void Add(SetupStepId id, SetupState state, string? code)
    {
        lock (_gate)
        {
            _last[id] = new RecordedStep(state, code, _last.TryGetValue(id, out var before) ? before.Count + 1 : 1);
        }
    }

    /// <summary>Marks the ledger as read.</summary>
    public void MarkLoaded()
    {
        lock (_gate)
        {
            Loaded = true;
        }
    }
}
