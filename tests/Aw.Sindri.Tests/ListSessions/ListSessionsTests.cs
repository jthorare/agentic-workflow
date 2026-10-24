using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.ListSessions;
using Aw.Sindri.Tests.Sessions;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.ListSessions;

/// <summary>GET /sessions (collector spec CA-1): the snapshot as rows, scoped and trimmed to the caller.</summary>
public sealed class ListSessionsTests
{
    private readonly SessionsRig _r = new();

    private ListSessionsService Service() => new(_r.Snapshot, _r.Catalog, _r.Base.Profiles, _r.Base.Callers, _r.Base.Clock, _r.LaunchViews);

    private async Task<SessionList> List(ListSessionsRequest? request = null) => (await Service().Handle(request ?? new ListSessionsRequest(), SessionsRig.Token)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_lists_every_session_for_a_human_with_its_human_fields_and_the_head_counts()
    {
        _ = _r.Publish(SessionsRig.Three);
        using var human = _r.Human();

        var list = await List();

        Assert.Equal([$"claude-code/{SessionsRig.ClaudeId}", $"codex/{SessionsRig.CodexId}", "cursor/pid-333-100"], list.Sessions.Select(s => s.Ref), StringComparer.Ordinal);
        var claude = list.Sessions[0];
        Assert.Equal(("Claude Code", true, SessionState.Running, StateSource.Record, "private title", 111), (claude.ProviderLabel, claude.IdKnown, claude.State, claude.StateSource, claude.Title, claude.Pid));
        Assert.Equal(("tmux", "tmux · Warp", "default", 1, "warp", "main", "%3"), (claude.Host.Kind, claude.Host.Label, claude.Host.SocketKind, claude.Host.AttachedClients, claude.Host.Terminal, claude.Host.Session, claude.Host.Pane));
        Assert.Equal((new SessionLane("sindri", "ideas"), ".claude/worktrees/ideas", "api"), (claude.Lane, claude.Worktree, claude.Repo!.Value.Value));
        Assert.Equal(new SessionFocus("none", Possible: false, "not-built"), claude.Focus);
        Assert.Equal(new SessionCounts(Live: 3, Asking: 1, OutsideWorkflow: 2), list.Counts);
        Assert.Null(list.Empty);
        Assert.Equal(SessionData.T0.AddMinutes(5), list.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_row_carries_the_launch_instance_state_and_attempt()
    {
        var instance = InstanceAddress.From("ITM-00000000000000000000000000000001/spec/1");
        await _r.Launches.Item(repo: null);
        await _r.Launches.Start(LaunchId.From(SessionsRig.Launch), instance: instance, state: "build", attempt: 2);
        _ = _r.Publish(SessionsRig.Three);
        using var human = _r.Human();

        var list = await List();

        Assert.Equal(new LaunchStep(instance, "build", 2), list.Sessions[2].Launch);
        Assert.Null(list.Sessions[0].Launch);   // Claude was not launched by Sindri
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_still_serves_rows_with_launch_null_when_the_launches_view_cannot_be_read()
    {
        _r.LaunchViews = new SessionsRig.UnreadableLaunches();
        _ = _r.Publish(SessionsRig.Three);
        using var human = _r.Human();

        var list = await List();

        Assert.Equal(3, list.Sessions.Count);
        Assert.All(list.Sessions, s => Assert.Null(s.Launch));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_gives_a_session_caller_every_session_with_its_human_fields_too()
    {
        _ = _r.Publish(SessionsRig.Three, ended: [new EndedSession(SessionsRig.Claude(), SessionData.T0.AddMinutes(4)), new EndedSession(SessionsRig.Codex(), SessionData.T0.AddMinutes(3))]);
        using var session = _r.SessionOf("api");

        var list = await List();

        Assert.Equal(3, list.Sessions.Count);
        var row = list.Sessions[0];
        Assert.Equal($"claude-code/{SessionsRig.ClaudeId}", row.Ref);
        Assert.Equal(("private title", 111, "main", "%3"), (row.Title, row.Pid, row.Host.Session, row.Host.Pane));
        Assert.Equal(("tmux", "default", 1, "warp"), (row.Host.Kind, row.Host.SocketKind, row.Host.AttachedClients, row.Host.Terminal));
        Assert.Equal(2, list.RecentlyEnded.Count);
        Assert.Equal(["claude", "--resume", SessionsRig.ClaudeId], list.RecentlyEnded[0].Resume);
        Assert.Equal(new SessionCounts(Live: 3, Asking: 1, OutsideWorkflow: 2), list.Counts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_shows_a_session_caller_with_no_repo_every_session()
    {
        _ = _r.Publish(SessionsRig.Three);
        using var session = _r.SessionWithoutRepo();

        var list = await List();

        Assert.Equal(3, list.Sessions.Count);
        Assert.Equal(3, list.Counts.Live);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_needs_a_caller()
    {
        _ = _r.Publish(SessionsRig.Three);

        var result = await Service().Handle(new ListSessionsRequest(), SessionsRig.Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("provider", 1)]
    [InlineData("state", 1)]
    [InlineData("repo", 1)]
    [InlineData("in-workflow", 1)]
    [InlineData("outside-workflow", 2)]
    public async Task ListSessions_filters_by_provider_state_repo_and_workflow(string filter, int expected)
    {
        _ = _r.Publish(SessionsRig.Three);
        using var human = _r.Human();
        var request = filter switch
        {
            "provider" => new ListSessionsRequest(Provider: AgentId.From("codex")),
            "state" => new ListSessionsRequest(State: SessionState.Idle),
            "repo" => new ListSessionsRequest(Repo: RepoName.From("web")),
            "in-workflow" => new ListSessionsRequest(InWorkflow: true),
            _ => new ListSessionsRequest(InWorkflow: false),
        };

        var list = await List(request);

        Assert.Equal(expected, list.Sessions.Count);
        Assert.Equal(3, list.Counts.Live);   // the head counts ignore the filters
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_gives_a_human_the_resume_argv_of_a_recently_ended_session_that_has_one()
    {
        _ = _r.Publish([], ended: [new EndedSession(SessionsRig.Claude(), SessionData.T0.AddMinutes(4)), new EndedSession(SessionsRig.Codex(), SessionData.T0.AddMinutes(3))]);
        using var human = _r.Human();

        var list = await List();

        Assert.Equal(["claude", "--resume", SessionsRig.ClaudeId], list.RecentlyEnded[0].Resume);
        Assert.Null(list.RecentlyEnded[1].Resume);   // codex has no resume form in the catalog
        Assert.Equal(SessionData.T0.AddMinutes(4), list.RecentlyEnded[0].EndedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_gives_no_resume_argv_for_an_id_the_agent_never_reported()
    {
        var unknown = SessionData.Session("claude-code", "pid-9-100") with { IdKnown = false };
        _ = _r.Publish([], ended: [new EndedSession(unknown, SessionData.T0)]);
        using var human = _r.Human();

        Assert.Null((await List()).RecentlyEnded[0].Resume);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_before_the_first_pass_is_an_empty_list_with_its_sentence()
    {
        using var human = _r.Human();

        var list = await List();

        Assert.Equal(("No agent sessions are running.", 0, _r.Base.Clock.GetUtcNow()), (list.Empty, list.Sessions.Count, list.AsOf));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_carries_a_failed_source_as_a_problem_beside_the_list()
    {
        var problem = new SessionProblem("claude-records", "SND-SESSION-001", "The claude-records source couldn't be read in the last pass");
        _ = _r.Publish(SessionsRig.Three, problems: [problem]);
        using var human = _r.Human();

        var list = await List();

        Assert.Equal([problem], list.Problems);
        Assert.Equal(3, list.Sessions.Count);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(UnavailableReason.UnsupportedPlatform, "unsupported-platform")]
    [InlineData(UnavailableReason.ProcUnreadable, "proc-unreadable")]
    [InlineData(UnavailableReason.Disabled, "disabled")]
    public async Task ListSessions_answers_SND_SESSION_002_when_the_collector_cannot_run_and_shows_no_stale_rows(UnavailableReason reason, string detail)
    {
        var problem = new SessionProblem("processes", "SND-SESSION-002", $"Sindri can't see agent sessions on this system: {detail}") { Reason = reason };
        _ = _r.Publish(SessionsRig.Three, problems: [problem]);
        using var human = _r.Human();

        var result = await Service().Handle(new ListSessionsRequest(), SessionsRig.Token);

        Assert.True(result.IsError);
        Assert.Equal(("SND-SESSION-002", detail), (result.Error.Code, result.Error.Message));
        Assert.True(UnavailableReasons.TryFrom(result.Error, out var read) && read == reason);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ListSessions_keeps_a_test_override_as_a_problem_row_not_a_replacement()
    {
        var problem = new SessionProblem("collector", "SND-SESSION-003", "A test override is set in this run: the sessions shown may be fixtures");
        _ = _r.Publish(SessionsRig.Three, problems: [problem]);
        using var human = _r.Human();

        var list = await List();

        Assert.Equal(3, list.Sessions.Count);
        Assert.Equal([problem], list.Problems);
    }
}
