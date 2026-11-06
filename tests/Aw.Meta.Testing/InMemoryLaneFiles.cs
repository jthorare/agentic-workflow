using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ILaneFiles: the one fake, kept honest by LaneFilesContract.</summary>
public sealed class InMemoryLaneFiles : ILaneFiles
{
    private readonly Dictionary<string, LaneRecord> _records = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LaneHookTrace> _traces = new(StringComparer.Ordinal);

    /// <summary>Puts a lane's record in place.</summary>
    /// <param name="record">The record.</param>
    public void Put(LaneRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.Lane] = record;
    }

    /// <summary>Puts a lane's hook trace in place.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="trace">The trace.</param>
    public void PutTrace(string lane, LaneHookTrace trace) => _traces[lane] = trace;

    /// <inheritdoc />
    public ValueTask<Result<LaneHookTrace?>> HookTrace(string lane, CancellationToken cancellationToken) =>
        Done.Of(_traces.GetValueOrDefault(lane));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> RecordSession(string lane, Core.Ids.SessionId session, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (lane.Length == 0 || lane.All(static c => c == '.') || !lane.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
        {
            return Done.Failed<Unit>(MetaErrors.UnknownLane.With($"'{lane}' is not a lane name"));
        }

        _records[lane] = (_records.GetValueOrDefault(lane) ?? new LaneRecord(lane, Session: null, Agent: null)) with { Session = session, RecordedAt = at, Ambiguous = false };
        return Done.Unit();
    }

    /// <inheritdoc />
    public ValueTask<Result<LaneRecord?>> Record(string lane, CancellationToken cancellationToken) =>
        Done.Of(_records.GetValueOrDefault(lane));
}
