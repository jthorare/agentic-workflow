using Aw.Kernel;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

internal sealed class FakeWrapCalls(TimeProvider clock) : IWrapCalls
{
    private readonly List<(DateTimeOffset At, SessionId? Session)> _beats = [];
    private readonly List<(int Code, SessionId? Session, string Proof)> _exits = [];

    public IReadOnlyList<(DateTimeOffset At, SessionId? Session)> Beats { get { lock (_beats) { return [.. _beats]; } } }

    public IReadOnlyList<(int Code, SessionId? Session, string Proof)> Exits { get { lock (_exits) { return [.. _exits]; } } }

    public Error? Refuse { get; set; }

    private Result<Unit> Answer() => Refuse is { } error ? error : Unit.Value;

    public ValueTask<Result<Unit>> Heartbeat(LaunchId launch, LaunchSecret proof, SessionId? session, CancellationToken cancellationToken)
    {
        lock (_beats)
        {
            _beats.Add((clock.GetUtcNow(), session));
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
}
