using Aw.Kernel;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Line;

/// <summary>The in-memory <see cref="IOwnerDecisions"/>: stores what it is given, once for the same run, packet and answer. A call can be made to fail.</summary>
public sealed class InMemoryOwnerDecisions : IOwnerDecisions
{
    private readonly Error? _error;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly List<(InstanceAddress Run, string Packet, IReadOnlyList<OwnerDecision> Decisions)> _calls = [];

    /// <summary>Answers every call.</summary>
    public InMemoryOwnerDecisions()
    {
        _error = null;
    }

    /// <summary>Fails every call with the error.</summary>
    /// <param name="error">The error.</param>
    public InMemoryOwnerDecisions(Error error)
    {
        _error = error;
    }

    /// <summary>The run, packet hash and decisions of each call, in order.</summary>
    public IReadOnlyList<(InstanceAddress Run, string Packet, IReadOnlyList<OwnerDecision> Decisions)> Calls => _calls;

    /// <inheritdoc/>
    public ValueTask<Result<OwnerDecisionsIngested>> Ingest(InstanceAddress run, string packet, IReadOnlyList<OwnerDecision> decisions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add((run, packet, decisions));
        if (_error is { } error)
        {
            return ValueTask.FromResult<Result<OwnerDecisionsIngested>>(error);
        }

        var stored = decisions.Count(d => _seen.Add($"{run.Value}|{packet}|{d.Answer}|{d.Text}"));
        return ValueTask.FromResult(Results.Success(new OwnerDecisionsIngested(stored, Skipped: null)));
    }
}
