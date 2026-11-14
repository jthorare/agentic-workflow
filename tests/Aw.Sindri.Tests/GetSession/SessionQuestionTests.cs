using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Features.ListSessions;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.GetSession;

/// <summary>'question' on the session reads (Agents page, issue 3082): the last 3 non-empty pane lines while the session asks, read per request, null in every other case.</summary>
public sealed class SessionQuestionTests
{
    private const string Socket = "/tmp/tmux-1000/default";
    private const string Pane = "%3";

    private static readonly SessionRef _claude = new(AgentId.From("claude-code"), SessionId.From(SessionsRig.ClaudeId));

    private readonly SessionsRig _r = new();

    private GetSessionService One() => new(_r.Snapshot, _r.Catalog, _r.Base.Profiles, _r.Base.Callers, _r.LaunchViews, _r.Sandboxes, _r.Questions(), _r.Facts());

    private ListSessionsService Many() => new(_r.Snapshot, _r.Catalog, _r.Base.Profiles, _r.Base.Callers, _r.Base.Clock, _r.LaunchViews, _r.Sandboxes, _r.Questions(), _r.Facts());

    private async Task<SessionRow> Row(SessionRef reference)
    {
        using var human = _r.Human();
        var one = (await One().Handle(new GetSessionRequest(reference), SessionsRig.Token)).Value.Session;
        var list = (await Many().Handle(new ListSessionsRequest(null, null, null, null), SessionsRig.Token)).Value.Sessions.Single(s => s.Ref == reference.ToString());
        Assert.Equal(one.Question, list.Question);   // both reads say the same
        return one;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_asking_session_in_tmux_shows_the_last_three_non_empty_pane_lines()
    {
        _ = _r.ClaudeAsking("old\n\nDo you want to run Bash?\n\n  1. Yes\n  2. No\n\n\n");

        var row = await Row(_claude);

        Assert.Equal("Do you want to run Bash?\n  1. Yes\n  2. No", row.Question);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pane_with_fewer_lines_gives_all_of_them_and_escapes_are_stripped()
    {
        _ = _r.ClaudeAsking("\u001b[31mContinue?\u001b[0m\n");

        Assert.Equal("Continue?", (await Row(_claude)).Question);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_that_is_not_asking_has_no_question_and_its_pane_is_not_read()
    {
        _ = _r.ClaudeAsking("Do you want to run Bash?\n");
        _ = _r.Publish([SessionsRig.Claude(), SessionsRig.Codex(), SessionsRig.Cursor()]);   // running now

        var row = await Row(_claude);

        Assert.Null(row.Question);
        Assert.Equal(0, _r.Panes.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_asking_session_outside_tmux_has_no_question_and_keeps_its_tool()
    {
        _ = _r.Publish(SessionsRig.Three);   // the Codex session asks for Bash under Warp

        var row = await Row(new SessionRef(AgentId.From("codex"), SessionId.From(SessionsRig.CodexId)));

        Assert.Null(row.Question);
        Assert.Equal("Bash", row.Asking?.Tool);
        Assert.Equal(0, _r.Panes.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pane_that_cannot_be_read_gives_null_not_an_error()
    {
        _ = _r.ClaudeAsking("Do you want to run Bash?\n");
        _ = _r.Panes.TimingOut(Socket);

        var row = await Row(_claude);

        Assert.Null(row.Question);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pane_that_is_gone_gives_null()
    {
        _ = _r.ClaudeAsking("Do you want to run Bash?\n");
        var moved = SessionsRig.Claude() with { State = SessionState.Asking, Reason = AskingReason.Approval, Host = SessionsRig.Claude().Host with { Tmux = new TmuxFacts(Socket, "default", "main", "%77") } };
        _ = _r.Publish([moved, SessionsRig.Codex(), SessionsRig.Cursor()]);

        Assert.Null((await Row(_claude)).Question);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pane_whose_process_is_not_the_session_gives_null()
    {
        _ = _r.ClaudeAsking("Do you want to run Bash?\n");
        _ = _r.Panes.With(Socket, Pane, 999, "someone else's pane\n");   // the pane's first process (999) is not an ancestor of pid 111

        Assert.Null((await Row(_claude)).Question);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_read_asks_the_pane_again_so_nothing_is_kept()
    {
        _ = _r.ClaudeAsking("first?\n");
        using var human = _r.Human();
        var service = One();

        var first = (await service.Handle(new GetSessionRequest(_claude), SessionsRig.Token)).Value.Session.Question;
        _ = _r.Panes.With(Socket, Pane, 50, "second?\n");
        var second = (await service.Handle(new GetSessionRequest(_claude), SessionsRig.Token)).Value.Session.Question;

        Assert.Equal(("first?", "second?"), (first, second));
    }
}
