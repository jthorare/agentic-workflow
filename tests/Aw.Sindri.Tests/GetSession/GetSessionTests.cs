using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.GetSession;

/// <summary>GET /sessions/{provider}/{id} (collector spec CA-2, CA-5): the row, and for a human the directory, the socket and the state history.</summary>
public sealed class GetSessionTests
{
    private static readonly SessionRef _claude = new(AgentId.From("claude-code"), SessionId.From(SessionsRig.ClaudeId));

    private readonly SessionsRig _r = new();

    private GetSessionService Service() => new(_r.Snapshot, _r.Catalog, _r.Base.Profiles, _r.Base.Callers);

    private static SessionStateEntry Entry(SessionState state, int minute, AskingReason? reason = null) => new(state, reason, SessionData.T0.AddMinutes(minute), StateSource.Hook);

    [Fact, Trait("Lane", "unit")]
    public async Task GetSession_gives_a_human_the_directory_the_socket_and_the_state_history()
    {
        var history = new Dictionary<SessionRef, IReadOnlyList<SessionStateEntry>> { [_claude] = [Entry(SessionState.Idle, 0), Entry(SessionState.Running, 1)] };
        _ = _r.Publish(SessionsRig.Three);
        _r.Snapshot.Publish(_r.Snapshot.Current! with { History = history });
        using var human = _r.Human();

        var view = (await Service().Handle(new GetSessionRequest(_claude), SessionsRig.Token)).Value;

        Assert.Equal(("/work/api/.claude/worktrees/ideas", "/tmp/tmux-1000/default", "main", "%3", "private title", 111),
            (view.Cwd, view.Session.Host.Socket, view.Session.Host.Session, view.Session.Host.Pane, view.Session.Title, view.Session.Pid));
        Assert.Equal([SessionState.Idle, SessionState.Running], view.StateHistory!.Select(h => h.State));
        Assert.Equal(SessionData.T0.AddMinutes(5), view.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetSession_hides_cwd_socket_and_history_from_a_session_caller()
    {
        var history = new Dictionary<SessionRef, IReadOnlyList<SessionStateEntry>> { [_claude] = [Entry(SessionState.Running, 1)] };
        _ = _r.Publish(SessionsRig.Three);
        _r.Snapshot.Publish(_r.Snapshot.Current! with { History = history });
        using var session = _r.SessionOf("api");

        var view = (await Service().Handle(new GetSessionRequest(_claude), SessionsRig.Token)).Value;

        Assert.All<object?>([view.Cwd, view.StateHistory, view.Session.Host.Socket, view.Session.Host.Session, view.Session.Host.Pane, view.Session.Title, view.Session.Pid], Assert.Null);
        Assert.Equal(($"claude-code/{SessionsRig.ClaudeId}", "tmux", "default"), (view.Session.Ref, view.Session.Host.Kind, view.Session.Host.SocketKind));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetSession_answers_SND_SESSION_404_for_an_unknown_ref()
    {
        _ = _r.Publish(SessionsRig.Three);
        using var human = _r.Human();

        var result = await Service().Handle(new GetSessionRequest(new SessionRef(AgentId.From("codex"), SessionId.From("nope"))), SessionsRig.Token);

        Assert.Equal("SND-SESSION-404", result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetSession_answers_SND_SESSION_404_for_a_session_outside_the_callers_repo_so_it_cannot_probe()
    {
        _ = _r.Publish(SessionsRig.Three);
        using var session = _r.SessionOf("web");

        var outside = await Service().Handle(new GetSessionRequest(_claude), SessionsRig.Token);
        var unknown = await Service().Handle(new GetSessionRequest(new SessionRef(AgentId.From("codex"), SessionId.From("nope"))), SessionsRig.Token);

        Assert.Equal((SindriErrors.SessionNotFound.Code, SindriErrors.SessionNotFound.Code), (outside.Error.Code, unknown.Error.Code));
        Assert.Equal(unknown.Error.Message, outside.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetSession_does_not_find_a_session_that_has_ended()
    {
        _ = _r.Publish([], ended: [new EndedSession(SessionsRig.Claude(), SessionData.T0)]);
        using var human = _r.Human();

        Assert.Equal("SND-SESSION-404", (await Service().Handle(new GetSessionRequest(_claude), SessionsRig.Token)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetSession_answers_the_collectors_unavailable_error_as_the_list_does()
    {
        _ = _r.Publish(SessionsRig.Three, problems: [new SessionProblem("collector", "SND-SESSION-002", "Sindri can't see agent sessions on this system: disabled") { Reason = UnavailableReason.Disabled }]);
        using var human = _r.Human();

        var result = await Service().Handle(new GetSessionRequest(_claude), SessionsRig.Token);

        Assert.Equal(("SND-SESSION-002", "disabled"), (result.Error.Code, result.Error.Message));
    }
}
