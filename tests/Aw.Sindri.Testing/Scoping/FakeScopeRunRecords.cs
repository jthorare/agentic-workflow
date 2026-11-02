using Aw.Kernel;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>
/// The in-memory <see cref="IScopeRunRecords"/>: the records it was given by instance, oldest first. An instance it holds none for has launched none. A call can be made to fail.
/// </summary>
public sealed class FakeScopeRunRecords : IScopeRunRecords
{
    private readonly Dictionary<string, List<ScopeRunRecord>> _runs = new(StringComparer.Ordinal);
    private readonly Error? _error;
    private readonly List<InstanceAddress> _calls = [];

    /// <summary>Answers every call from what it holds.</summary>
    public FakeScopeRunRecords()
    {
        _error = null;
    }

    /// <summary>Fails every call with the error.</summary>
    /// <param name="error">The error.</param>
    public FakeScopeRunRecords(Error error)
    {
        _error = error;
    }

    /// <summary>The instance of each call, in order.</summary>
    public IReadOnlyList<InstanceAddress> Calls => _calls;

    /// <summary>Holds records for an instance, after the ones it already holds.</summary>
    /// <param name="run">The instance's address.</param>
    /// <param name="records">The records, oldest first.</param>
    /// <returns>The same instance.</returns>
    public FakeScopeRunRecords With(string run, params ScopeRunRecord[] records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (!_runs.TryGetValue(run, out var held))
        {
            held = [];
            _runs[run] = held;
        }

        held.AddRange(records);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ScopeRunRecord>>> Of(InstanceAddress run, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add(run);
        if (_error is { } error)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<ScopeRunRecord>>>(error);
        }

        return ValueTask.FromResult(Results.Success<IReadOnlyList<ScopeRunRecord>>(_runs.TryGetValue(run.Value, out var held) ? [.. held] : []));
    }
}
