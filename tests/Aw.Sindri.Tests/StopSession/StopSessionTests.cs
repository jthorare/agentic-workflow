using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.StartSession;
using Aw.Sindri.Features.StopSession;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.StartSession;

namespace Aw.Sindri.Tests.StopSession;

/// <summary>
/// StopSession (agents spec §15.2, issue 3147): ends a one-off session Sindri started ('kill-session -t sindri-&lt;actId&gt;'). Human only; a session Sindri did not start is SND-AGENT-019;
/// a session that is already gone counts as stopped.
/// </summary>
public sealed class StopSessionTests : IDisposable
{
    private const string Act = "k3j9x0a1";

    private readonly StartRig _r = new();

    public void Dispose() => _r.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SessionRef Ref(string id) => new(AgentId.From("claude-code"), SessionId.From(id));

    private readonly InMemorySessionSnapshot _snapshot = new();

    private StopSessionService Service() => new(_r.Book, _r.Host, _r.Base.Callers, _snapshot);

    private async Task StartOneOff() =>
        _ = (await _r.Service().Handle(new StartSessionRequest(RepoName.From("demo"), AgentId.From("claude-code"), "hi", false, ActId.From(Act)), Token)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task Stop_ends_a_session_Sindri_started_and_answers_it_ended()
    {
        await StartOneOff();

        var stopped = await Service().Handle(new StopSessionRequest(Ref("sindri-" + Act)), Token);

        Assert.True(stopped.IsSuccess, stopped.IsError ? stopped.Error.ToString() : null);
        Assert.Equal("ended", stopped.Value.State);
        Assert.Equal(Act, stopped.Value.ActId.Value);
        Assert.False(await _r.Host.Exists(new HostRef(HostKind.Tmux, "sindri-" + Act), Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Stop_of_a_session_Sindri_did_not_start_is_SND_AGENT_019_and_kills_nothing()
    {
        await StartOneOff();

        var refused = await Service().Handle(new StopSessionRequest(Ref("3970893f-aaaa-bbbb")), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentNotStartedHere.Code, refused.Error.Code);
        Assert.True(await _r.Host.Exists(new HostRef(HostKind.Tmux, "sindri-" + Act), Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Stop_of_a_sindri_name_no_start_recorded_is_SND_AGENT_019()
    {
        var refused = await Service().Handle(new StopSessionRequest(Ref("sindri-zzzzzzzz")), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentNotStartedHere.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Stop_by_an_agent_session_is_SND_AUTH_002_and_kills_nothing()
    {
        await StartOneOff();

        using var _ = _r.AsSession();
        var refused = await Service().Handle(new StopSessionRequest(Ref("sindri-" + Act)), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Assert.True(await _r.Host.Exists(new HostRef(HostKind.Tmux, "sindri-" + Act), Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Stop_of_a_session_that_is_already_gone_counts_as_stopped()
    {
        await StartOneOff();
        _ = await _r.Host.Stop(new HostRef(HostKind.Tmux, "sindri-" + Act), Token);

        var stopped = await Service().Handle(new StopSessionRequest(Ref("sindri-" + Act)), Token);

        Assert.True(stopped.IsSuccess, stopped.IsError ? stopped.Error.ToString() : null);
        Assert.Equal("ended", stopped.Value.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Stop_by_the_provider_session_id_of_a_row_on_Sindris_own_socket_ends_that_session()
    {
        await StartOneOff();
        var session = new SessionRef(AgentId.From("claude-code"), SessionId.From("3970893f-aaaa-bbbb"));
        _snapshot.Publish(new SessionSnapshot(
            [new ObservedSession(session, 4121, 77, SessionState.Running, StateSource.Hook, DateTimeOffset.UnixEpoch)
            {
                Host = HostInfo.Unknown with { Tmux = new TmuxFacts("/tmp/s", "sindri", "sindri-" + Act, "%1") },
            }],
            [], [], DateTimeOffset.UnixEpoch));

        var stopped = await Service().Handle(new StopSessionRequest(session), Token);

        Assert.True(stopped.IsSuccess, stopped.IsError ? stopped.Error.ToString() : null);
        Assert.Equal("sindri-" + Act, stopped.Value.Id.Value);
        Assert.False(await _r.Host.Exists(new HostRef(HostKind.Tmux, "sindri-" + Act), Token));
    }
}
