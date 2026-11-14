using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Features.ListSessions;
using Aw.Sindri.Features.StartSession;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.GetSession;

/// <summary>'branch' and the start prompt's title on the session reads (Agents page, issue 3082).</summary>
public sealed class SessionBranchAndTitleTests
{
    private const string Cwd = "/work/api/.claude/worktrees/ideas";

    private static readonly SessionRef _claude = new(AgentId.From("claude-code"), SessionId.From(SessionsRig.ClaudeId));

    private readonly SessionsRig _r = new();

    private async Task<SessionRow> Row()
    {
        using var human = _r.Human();
        var one = (await new GetSessionService(_r.Snapshot, _r.Catalog, _r.Base.Profiles, _r.Base.Callers, _r.LaunchViews, _r.Sandboxes, _r.Questions(), _r.Facts()).Handle(new GetSessionRequest(_claude), SessionsRig.Token)).Value.Session;
        var list = (await new ListSessionsService(_r.Snapshot, _r.Catalog, _r.Base.Profiles, _r.Base.Callers, _r.Base.Clock, _r.LaunchViews, _r.Sandboxes, _r.Questions(), _r.Facts()).Handle(new ListSessionsRequest(null, null, null, null), SessionsRig.Token)).Value.Sessions.Single(s => s.Ref == _claude.ToString());
        Assert.Equal((one.Branch, one.Title), (list.Branch, list.Title));   // both reads say the same
        return one;
    }

    private SessionsRig Started(string sessionName, string? recordTitle)
    {
        var session = SessionsRig.Claude();
        _ = _r.Publish([session with { Title = recordTitle, Host = session.Host with { Tmux = session.Host.Tmux! with { Session = sessionName } } }, SessionsRig.Codex(), SessionsRig.Cursor()]);
        return _r;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_row_carries_the_branch_the_worktree_has_checked_out()
    {
        _ = _r.Publish(SessionsRig.Three);
        _ = _r.Branches.With(Cwd, "feat/gh-3146-session-changed");

        Assert.Equal("feat/gh-3146-session-changed", (await Row()).Branch);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_detached_or_unreadable_worktree_has_no_branch()
    {
        _ = _r.Publish(SessionsRig.Three);   // the fake knows no branch for the directory

        Assert.Null((await Row()).Branch);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_with_no_directory_has_no_branch_and_nothing_is_read()
    {
        _ = _r.Publish([SessionsRig.Claude() with { Cwd = null }, SessionsRig.Codex(), SessionsRig.Cursor()]);

        Assert.Null((await Row()).Branch);
        Assert.DoesNotContain(Cwd, _r.Branches.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_Sindri_started_without_a_title_shows_the_title_of_its_start_prompt()
    {
        _r.Starts.Keep(ActId.From("abcd1234"), new StartedEntry(TmuxPaneId.From("%3"), null) { Title = "Fix the login timeout" });
        _ = Started("sindri-abcd1234", recordTitle: null);

        Assert.Equal("Fix the login timeout", (await Row()).Title);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_title_the_agent_gave_wins_over_the_start_prompt()
    {
        _r.Starts.Keep(ActId.From("abcd1234"), new StartedEntry(TmuxPaneId.From("%3"), null) { Title = "Fix the login timeout" });
        _ = Started("sindri-abcd1234", recordTitle: "api work");

        Assert.Equal("api work", (await Row()).Title);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_Sindri_did_not_start_has_no_made_up_title()
    {
        _r.Starts.Keep(ActId.From("abcd1234"), new StartedEntry(TmuxPaneId.From("%3"), null) { Title = "Fix the login timeout" });
        _ = Started("main", recordTitle: null);

        Assert.Null((await Row()).Title);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Fix the login timeout", "Fix the login timeout")]
    [InlineData("\n\n  first line  \nsecond line", "first line")]
    [InlineData("\r\nwindows line\r\nnext", "windows line")]
    [InlineData("   \n\t\n", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void The_start_title_is_the_first_non_empty_line_trimmed(string? prompt, string? expected) => Assert.Equal(expected, StartTitle.Of(prompt));

    [Fact, Trait("Lane", "unit")]
    public void The_start_title_is_cut_to_80_characters()
    {
        var title = StartTitle.Of(new string('x', 200) + "\nmore");

        Assert.Equal(new string('x', 80), title);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_start_title_never_splits_a_surrogate_pair_at_the_cut()
    {
        var title = StartTitle.Of(new string('x', 79) + "\U0001F600tail")!;

        Assert.True(title.Length <= 80);
        Assert.True(!char.IsHighSurrogate(title[^1]));
    }
}
