using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Xunit;

namespace Aw.Sindri.Testing.Launch;

/// <summary>The contract every <see cref="IHostedSessions"/> honours, run against the fake and the T3 host.</summary>
public abstract class HostedSessionsContract
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-0000000000000000000000000000000a");

    /// <summary>The port under test, with nothing started.</summary>
    /// <returns>The port.</returns>
    protected abstract IHostedSessions Create();

    /// <summary>Starts a session for <paramref name="launch"/> on <paramref name="sessions"/>' host.</summary>
    /// <param name="sessions">The port from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The work.</returns>
    protected abstract Task Start(IHostedSessions sessions, LaunchId launch);

    /// <summary>Ends the session outside Sindri (the user archived or deleted it).</summary>
    /// <param name="sessions">The port from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    protected abstract void EndOutside(IHostedSessions sessions, LaunchId launch);

    /// <summary>Nothing started, nothing reported.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Nothing_is_reported_before_a_start() => Expect.Equal(0, (await Create().Check(CancellationToken.None)).Count);

    /// <summary>A started session is reported alive.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_started_session_is_alive()
    {
        var sessions = Create();
        await Start(sessions, _launch);

        Expect.Sequence([new HostedSession(_launch, Alive: true)], await sessions.Check(CancellationToken.None));
    }

    /// <summary>A session ended outside Sindri is reported gone.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_session_ended_outside_is_gone()
    {
        var sessions = Create();
        await Start(sessions, _launch);
        EndOutside(sessions, _launch);

        Expect.Sequence([new HostedSession(_launch, Alive: false)], await sessions.Check(CancellationToken.None));
    }

    /// <summary>A forgotten session is no longer reported, and forgetting again is safe.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_forgotten_session_is_not_reported()
    {
        var sessions = Create();
        await Start(sessions, _launch);

        sessions.Forget(_launch);
        sessions.Forget(_launch);

        Expect.Equal(0, (await sessions.Check(CancellationToken.None)).Count);
    }
}
