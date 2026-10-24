using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetSessionPreview;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.GetSessionPreview;

/// <summary>GET /sessions/{provider}/{id}/preview (collector spec CA-9, CI-10): the check order, the three reasons, the liveness walk, one read per pane, and nothing logged.</summary>
public sealed class GetSessionPreviewTests
{
    private const string Socket = "/tmp/tmux-1000/default";
    private const string Pane = "%3";

    private static readonly SessionRef _claude = new(AgentId.From("claude-code"), SessionId.From(SessionsRig.ClaudeId));

    private readonly SessionsRig _r = new();
    private readonly FakePaneReader _panes = new();
    private readonly FakeProcessTable _table = new();

    /// <summary>The Claude session (pid 111) runs below a shell (pid 50) which is the pane's first process.</summary>
    private GetSessionPreviewService Service()
    {
        _ = _r.Publish(SessionsRig.Three);
        _ = _table.With(SessionData.Proc(1, 0, "init")).With(SessionData.Proc(50, 1, "bash")).With(SessionData.Proc(60, 50, "node")).With(SessionData.Proc(111, 60, "claude"));
        _ = _panes.WithDefault(Socket, Pane, 50, "one\ntwo\n");
        return Build();
    }

    private GetSessionPreviewService Build() => new(_r.Snapshot, _r.Base.Callers, new PaneCaptures(_panes), _table, _r.Base.Clock);

    private static GetSessionPreviewRequest Ask(SessionRef? reference = null, DateTimeOffset? expect = null) => new(reference ?? _claude, expect);

    private static SessionPreviewUnavailable Unavailable(Kernel.Result<GetSessionPreviewResponse> result) => Assert.IsType<SessionPreviewUnavailable>(result.Value);

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_gives_a_human_the_pane_text_and_the_row_facts()
    {
        var service = Service();
        using var human = _r.Human();

        var preview = Assert.IsType<SessionPreview>((await service.Handle(Ask(), SessionsRig.Token)).Value);

        Assert.Equal(["one", "two"], preview.Lines);
        Assert.False(preview.Truncated);
        Assert.Equal((_r.Base.Clock.GetUtcNow(), SessionData.T0.AddMinutes(-30), "tmux", "tmux · Warp"), (preview.CapturedAt, preview.StartedAt, preview.Host.Kind, preview.Host.Label));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_answers_not_in_tmux()
    {
        var service = Service();
        using var human = _r.Human();

        var answer = Unavailable(await service.Handle(Ask(new SessionRef(AgentId.From("codex"), SessionId.From(SessionsRig.CodexId))), SessionsRig.Token));
        var plain = Unavailable(await service.Handle(Ask(new SessionRef(AgentId.From("cursor"), SessionId.From("pid-333-100"))), SessionsRig.Token));

        Assert.Equal((PreviewReason.NotInTmux, PreviewReason.NotInTmux, _r.Base.Clock.GetUtcNow()), (answer.Reason, plain.Reason, answer.CapturedAt));
        Assert.Equal(0, _panes.Reads);   // nothing was asked of tmux
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_maps_a_timed_out_read_to_timed_out()
    {
        _ = _panes.TimingOut(Socket);
        var service = Service();
        using var human = _r.Human();

        Assert.Equal(PreviewReason.TimedOut, Unavailable(await service.Handle(Ask(), SessionsRig.Token)).Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_answers_not_live_when_the_pane_is_gone()
    {
        var service = Service();
        var moved = SessionsRig.Claude();
        _ = _r.Publish([moved with { Host = moved.Host with { Tmux = new TmuxFacts(Socket, "default", "main", "%77") } }]);   // the pane id the snapshot holds is not on the server
        using var human = _r.Human();

        var answer = Unavailable(await service.Handle(Ask(), SessionsRig.Token));

        Assert.Equal(new SessionPreviewUnavailable(PreviewReason.NotLive, _r.Base.Clock.GetUtcNow()), answer);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_answers_not_live_when_the_pane_was_reused()
    {
        _ = _panes.With(Socket, Pane, 999, "someone else's text\n");   // the pane id now belongs to a shell that is not the session's ancestor
        var service = Service();
        using var human = _r.Human();

        var answer = Unavailable(await service.Handle(Ask(), SessionsRig.Token));

        Assert.Equal(PreviewReason.NotLive, answer.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_is_live_when_the_agent_is_a_child_of_the_pane_shell()
    {
        var service = Service();   // 111 (claude) -> 60 (node) -> 50 (bash, the pane's first process)
        using var human = _r.Human();

        _ = Assert.IsType<SessionPreview>((await service.Handle(Ask(), SessionsRig.Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_is_live_when_the_agent_is_the_pane_process_itself()
    {
        _ = _panes.With(Socket, Pane, 111, "direct\n");
        var service = Service();
        using var human = _r.Human();

        Assert.Equal(["direct"], Assert.IsType<SessionPreview>((await service.Handle(Ask(), SessionsRig.Token)).Value).Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_answers_not_live_when_the_pane_belongs_to_another_process()
    {
        _ = _panes.With(Socket, Pane, 70, "another pane's shell\n");
        var service = Service();
        _ = _table.With(SessionData.Proc(70, 1, "bash"));   // alive, but in a sibling tree
        using var human = _r.Human();

        Assert.Equal(PreviewReason.NotLive, Unavailable(await service.Handle(Ask(), SessionsRig.Token)).Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_answers_not_live_when_the_process_table_cannot_be_read()
    {
        var service = Service();
        _table.Fail();
        using var human = _r.Human();

        Assert.Equal(PreviewReason.NotLive, Unavailable(await service.Handle(Ask(), SessionsRig.Token)).Reason);   // never shows text it could not check
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_reports_an_unknown_ref_as_not_found()
    {
        var service = Service();
        using var human = _r.Human();

        var result = await service.Handle(Ask(new SessionRef(AgentId.From("codex"), SessionId.From("nope"))), SessionsRig.Token);

        Assert.Equal("SND-SESSION-404", result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_answers_sessions_unavailable_before_the_ref()
    {
        var service = Service();
        _ = _r.Publish(SessionsRig.Three, problems: [new SessionProblem("collector", "SND-SESSION-002", "Sindri can't see agent sessions on this system: disabled") { Reason = UnavailableReason.Disabled }]);
        Kernel.Result<GetSessionPreviewResponse> known, unknown, session;
        using (_r.Human())
        {
            known = await service.Handle(Ask(), SessionsRig.Token);
            unknown = await service.Handle(Ask(new SessionRef(AgentId.From("codex"), SessionId.From("nope"))), SessionsRig.Token);
        }

        using (_r.SessionOf("api"))
        {
            session = await service.Handle(Ask(), SessionsRig.Token);
        }

        Assert.Equal(["SND-SESSION-002", "SND-SESSION-002", "SND-SESSION-002"], [known.Error.Code, unknown.Error.Code, session.Error.Code]);   // never a 404: an unreadable collector is not an ended session
        Assert.Equal(0, _panes.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_conflicts_on_a_stale_startedAt()
    {
        var service = Service();
        using var human = _r.Human();

        var result = await service.Handle(Ask(expect: SessionData.T0.AddMinutes(-31)), SessionsRig.Token);

        Assert.Equal(SindriErrors.SessionConflict.Code, result.Error.Code);
        Assert.Equal(0, _panes.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_accepts_the_startedAt_the_caller_saw_and_none_at_all()
    {
        var service = Service();
        using var human = _r.Human();

        _ = Assert.IsType<SessionPreview>((await service.Handle(Ask(expect: SessionData.T0.AddMinutes(-30)), SessionsRig.Token)).Value);
        _ = Assert.IsType<SessionPreview>((await service.Handle(Ask(expect: null), SessionsRig.Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_matches_a_row_with_no_startedAt_on_its_ref_alone()
    {
        var service = Service();
        _ = _r.Publish([SessionsRig.Claude() with { StartedAt = null }]);
        using var human = _r.Human();

        var preview = Assert.IsType<SessionPreview>((await service.Handle(Ask(expect: null), SessionsRig.Token)).Value);
        var stale = await service.Handle(Ask(expect: SessionData.T0), SessionsRig.Token);   // the caller saw a start time this row never had

        Assert.Null(preview.StartedAt);
        Assert.Equal(SindriErrors.SessionConflict.Code, stale.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_answers_a_session_caller_like_a_human_for_an_unknown_and_a_stale_ref()
    {
        var service = Service();
        using var session = _r.SessionOf("api");

        var unknown = await service.Handle(Ask(new SessionRef(AgentId.From("codex"), SessionId.From("nope"))), SessionsRig.Token);
        var stale = await service.Handle(Ask(expect: SessionData.T0), SessionsRig.Token);

        Assert.Equal([SindriErrors.SessionNotFound.Code, SindriErrors.SessionConflict.Code], [unknown.Error.Code, stale.Error.Code]);
        Assert.Equal(0, _panes.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_refuses_a_call_with_no_caller()
    {
        var service = Service();

        Assert.Equal("SND-AUTH-002", (await service.Handle(Ask(), SessionsRig.Token)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_allows_one_capture_per_pane()
    {
        using var hold = new ManualResetEventSlim(initialState: false);
        _panes.Hold = hold;
        var service = Service();
        using var human = _r.Human();

        var first = service.Handle(Ask(), SessionsRig.Token).AsTask();
        await _panes.Started.WaitAsync(SessionsRig.Token);   // the first read is running
        var second = service.Handle(Ask(), SessionsRig.Token).AsTask();
        hold.Set();
        var answers = await Task.WhenAll(first, second);

        Assert.Equal(1, _panes.Reads);
        Assert.All(answers, static a => Assert.IsType<SessionPreview>(a.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_checks_expectStartedAt_per_request_while_a_capture_is_shared()
    {
        using var hold = new ManualResetEventSlim(initialState: false);
        _panes.Hold = hold;
        var service = Service();
        using var human = _r.Human();

        var first = service.Handle(Ask(), SessionsRig.Token).AsTask();
        await _panes.Started.WaitAsync(SessionsRig.Token);
        var stale = await service.Handle(Ask(expect: SessionData.T0), SessionsRig.Token);   // answers at once: its 409 never waits for the shared read
        hold.Set();
        var good = await first;

        Assert.Equal(SindriErrors.SessionConflict.Code, stale.Error.Code);
        _ = Assert.IsType<SessionPreview>(good.Value);
        Assert.Equal(1, _panes.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_reads_again_once_the_first_read_is_done()
    {
        var service = Service();
        using var human = _r.Human();

        _ = await service.Handle(Ask(), SessionsRig.Token);
        _ = await service.Handle(Ask(), SessionsRig.Token);

        Assert.Equal(2, _panes.Reads);   // sharing is for reads in flight, not a cache
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_writes_no_ledger_event()
    {
        var service = Service();
        using var human = _r.Human();
        var before = _r.Base.Store.Events.Count;

        _ = await service.Handle(Ask(), SessionsRig.Token);
        _ = await service.Handle(Ask(new SessionRef(AgentId.From("codex"), SessionId.From("nope"))), SessionsRig.Token);
        _ = await service.Handle(Ask(expect: SessionData.T0), SessionsRig.Token);

        Assert.Equal(before, _r.Base.Store.Events.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_serves_the_text_after_the_rules()
    {
        _ = _panes.With(Socket, Pane, 50, "\u001b[31m" + new string('a', 250) + "\u001b[0m\nsecond\n\n\n");
        var service = Service();
        using var human = _r.Human();

        var preview = Assert.IsType<SessionPreview>((await service.Handle(Ask(), SessionsRig.Token)).Value);

        Assert.Equal([200, 6], preview.Lines.Select(static l => l.Length));
        Assert.True(preview.Truncated);
    }
}
