using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

internal sealed class FakeWrapCalls(TimeProvider clock) : IWrapCalls
{
    private readonly List<(DateTimeOffset At, SessionId? Session)> _beats = [];
    private readonly List<(int Code, SessionId? Session, string Proof)> _exits = [];
    private readonly List<(int Code, SessionId? Session, string Proof, LimitEnd Limit)> _limitExits = [];
    private int _suspensionReads;

    public IReadOnlyList<(DateTimeOffset At, SessionId? Session)> Beats { get { lock (_beats) { return [.. _beats]; } } }

    public IReadOnlyList<(int Code, SessionId? Session, string Proof)> Exits { get { lock (_exits) { return [.. _exits]; } } }

    /// <summary>Each exit that carried a sandbox limit: the code, the session id, the proof and the limit.</summary>
    public IReadOnlyList<(int Code, SessionId? Session, string Proof, LimitEnd Limit)> LimitExits { get { lock (_limitExits) { return [.. _limitExits]; } } }

    /// <summary>What the suspension read answers, by the time it is asked; the default is "not suspended".</summary>
    public Func<DateTimeOffset, Result<bool>> Suspension { get; set; } = static _ => false;

    /// <summary>How often the suspension read was asked.</summary>
    public int SuspensionReads => Volatile.Read(ref _suspensionReads);

    public Error? Refuse { get; set; }

    /// <summary>When set, a heartbeat throws it (an unreachable control plane, a bad response) instead of answering.</summary>
    public Exception? ThrowOnBeat { get; set; }

    private Result<Unit> Answer() => Refuse is { } error ? error : Unit.Value;

    public ValueTask<Result<Unit>> Heartbeat(LaunchId launch, LaunchSecret proof, SessionId? session, CancellationToken cancellationToken)
    {
        lock (_beats)
        {
            _beats.Add((clock.GetUtcNow(), session));
        }

        if (ThrowOnBeat is { } thrown)
        {
            throw thrown;
        }

        return ValueTask.FromResult(Answer());
    }

    public ValueTask<Result<Unit>> Exit(LaunchId launch, LaunchSecret proof, int exitCode, SessionId? session, CancellationToken cancellationToken)
    {
        lock (_exits)
        {
            _exits.Add((exitCode, session, proof.Value));
        }

        return ValueTask.FromResult(Answer());
    }

    public ValueTask<Result<Unit>> ExitByLimit(LaunchId launch, LaunchSecret proof, int exitCode, SessionId? session, LimitEnd limit, CancellationToken cancellationToken)
    {
        lock (_limitExits)
        {
            _limitExits.Add((exitCode, session, proof.Value, limit));
        }

        return ValueTask.FromResult(Answer());
    }

    public ValueTask<Result<bool>> Suspended(LaunchId launch, LaunchSecret proof, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _suspensionReads);
        return ValueTask.FromResult(Suspension(clock.GetUtcNow()));
    }
}
