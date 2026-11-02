using Aw.Kernel;
using Aw.Sindri.Core;
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
    private readonly List<LimitEnd?> _limits = [];
    private readonly List<(LimitHitKind Kind, long Peak)> _hits = [];

    public IReadOnlyList<(DateTimeOffset At, SessionId? Session)> Beats { get { lock (_beats) { return [.. _beats]; } } }

    public IReadOnlyList<(int Code, SessionId? Session, string Proof)> Exits { get { lock (_exits) { return [.. _exits]; } } }

    /// <summary>The limit each exit carried, in order (null for an ordinary exit).</summary>
    public IReadOnlyList<LimitEnd?> Limits { get { lock (_exits) { return [.. _limits]; } } }

    /// <summary>The limit hits the wrap reported, in order.</summary>
    public IReadOnlyList<(LimitHitKind Kind, long Peak)> Hits { get { lock (_hits) { return [.. _hits]; } } }

    /// <summary>When true, <see cref="LimitHit"/> answers an error, as an unreachable control plane does.</summary>
    public bool LimitHitUnavailable { get; set; }

    /// <summary>What <see cref="Suspended"/> answers.</summary>
    public bool IsSuspended { get; set; }

    /// <summary>When true, <see cref="Suspended"/> answers an error, as an unreachable control plane does.</summary>
    public bool SuspendedUnavailable { get; set; }

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

    public ValueTask<Result<Unit>> Exit(LaunchId launch, LaunchSecret proof, WrapExit ended, CancellationToken cancellationToken)
    {
        lock (_exits)
        {
            _exits.Add((ended.ExitCode, ended.Session, proof.Value));
            _limits.Add(ended.Limit);
        }

        return ValueTask.FromResult(Answer());
    }

    public ValueTask<Result<bool>> Suspended(LaunchId launch, LaunchSecret proof, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<bool>>(SuspendedUnavailable ? SindriErrors.NotRunning.With("the control plane went away") : IsSuspended);

    public ValueTask<Result<Unit>> LimitHit(LaunchId launch, LaunchSecret proof, LimitHitKind kind, long peak, CancellationToken cancellationToken)
    {
        if (LimitHitUnavailable)
        {
            return ValueTask.FromResult<Result<Unit>>(SindriErrors.NotRunning.With("the control plane went away"));
        }

        lock (_hits)
        {
            _hits.Add((kind, peak));
        }

        return ValueTask.FromResult(Answer());
    }
}
