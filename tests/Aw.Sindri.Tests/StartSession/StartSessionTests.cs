using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.StartSession;

namespace Aw.Sindri.Tests.StartSession;

/// <summary>
/// StartSession (agents spec §15.8, A7): a one-off session is a tmux session named 'sindri-&lt;actId&gt;'. The refusals are SND-AGENT-012, 013, 014 and 017; a bad actId is a plain 400.
/// Only a 2xx result is stored under the actId, in one 'agent.op' event.
/// </summary>
public sealed class StartSessionTests : IDisposable
{
    private const string Act = "k3j9x0a1";
    private static readonly RepoName _repo = RepoName.From("demo");
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly AgentId _codex = AgentId.From("codex");

    private readonly StartRig _r = new();

    public void Dispose() => _r.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static StartSessionRequest Request(string? prompt = "fix the flaky test", bool worktree = true, string actId = Act, AgentId? agent = null) => new(_repo, agent ?? _claude, prompt, worktree, ActId.From(actId));

    [Fact, Trait("Lane", "unit")]
    public async Task Start_answers_starting_with_the_tmux_session_named_after_the_actId()
    {
        var started = await _r.Service().Handle(Request(), Token);

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        Assert.Equal(("sindri-" + Act, Act, "starting"), (started.Value.Id.Value, started.Value.ActId.Value, started.Value.State));
        Assert.Equal(("sindri-launch", "sindri-" + Act), (started.Value.Host.Socket, started.Value.Host.Session));
        Assert.Matches("^%[0-9]+$", started.Value.Host.Pane);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_starts_the_session_in_a_window_of_a_known_size_that_stays_when_the_agent_exits()
    {
        _ = await _r.Service().Handle(Request(), Token);

        var session = Assert.Single(_r.Host.Started);
        Assert.Equal("sindri-" + Act, session.Name);
        Assert.NotNull(session.Size);
        Assert.True(session.RemainOnExit);
        Assert.True(session.CleanEnvironment);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_runs_the_agent_in_the_new_worktree_and_in_the_repo_folder_without_one()
    {
        _ = await _r.Service().Handle(Request(worktree: true), Token);
        _ = await _r.Service().Handle(Request(worktree: false, actId: "second0001"), Token);

        Assert.Equal(["/wt/demo/" + Assert.Single(_r.Worktrees.Names), StartRig.RepoPath], _r.Host.Started.Select(s => s.WorkDir), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_names_the_worktree_oneoff_date_time_and_the_first_three_words_of_the_prompt()
    {
        _ = await _r.Service().Handle(Request(prompt: "Fix the flaky Checkout test, please"), Token);

        Assert.Matches(@"^oneoff-\d{6}-\d{4}-fix-the-flaky$", Assert.Single(_r.Worktrees.Names));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_names_the_worktree_after_the_agent_when_the_prompt_is_empty()
    {
        _ = await _r.Service().Handle(Request(prompt: null), Token);

        Assert.EndsWith("-claude-code", Assert.Single(_r.Worktrees.Names), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("claude-code", "-fix the thing")]
    [InlineData("codex", "-fix the thing")]
    [InlineData("claude-code", "line one\nline two with 'quotes' and \"doubles\"")]
    public async Task The_prompt_is_one_argv_element_after_double_dash(string agent, string prompt)
    {
        _ = await _r.Service().Handle(Request(prompt: prompt, agent: AgentId.From(agent)), Token);

        Assert.Equal([agent == "codex" ? "codex" : "claude", "--", prompt], Assert.Single(_r.Host.Started).Argv);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_without_a_prompt_starts_the_agent_at_its_own_prompt()
    {
        _ = await _r.Service().Handle(Request(prompt: null), Token);

        Assert.Equal(["claude"], Assert.Single(_r.Host.Started).Argv);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_session_environment_holds_only_the_allowed_variables()
    {
        _ = await _r.Service().Handle(Request(), Token);

        var env = Assert.Single(_r.Host.Started).Env;
        Assert.Equal(["ANTHROPIC_API_KEY", "AW_FLEET_DIR", "AW_SINDRI_ONEOFF", "AW_STATE_DIR", "HOME", "PATH", "TERM"], env.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal("1", env["AW_SINDRI_ONEOFF"]);   // why: the CLI inside never calls as the person (#3466)
        Assert.DoesNotContain("AW_SINDRI_TOKEN", env.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("GH_TOKEN", env.Keys, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("GH_TOKEN")]
    [InlineData("GITHUB_TOKEN")]
    [InlineData("GH_ENTERPRISE_TOKEN")]
    [InlineData("AW_FORGE_TOKEN")]
    [InlineData("AW_SINDRI_TOKEN")]
    [InlineData("AW_SINDRI_HOME")]
    [InlineData("AW_SINDRI_PORT")]
    [InlineData("OPENAI_API_KEY")]
    [InlineData("AWS_SECRET_ACCESS_KEY")]
    public async Task A_real_secret_or_Sindri_variable_never_reaches_a_Claude_session(string name)
    {
        _r.Environment.Values[name] = "value-" + name;   // why: the real names the scrub claims to drop, not stand-ins (#3470)

        _ = await _r.Service().Handle(Request(), Token);

        Assert.DoesNotContain(name, Assert.Single(_r.Host.Started).Env.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Codex_gets_its_own_sign_in_variable_and_not_Claudes()
    {
        _ = await _r.Service().Handle(Request(agent: _codex), Token);

        var env = Assert.Single(_r.Host.Started).Env;
        Assert.Contains("OPENAI_API_KEY", env.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", env.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_with_a_repo_not_in_the_profile_is_SND_AGENT_012()
    {
        var refused = await _r.Service().Handle(new StartSessionRequest(RepoName.From("nope"), _claude, "hi", Worktree: false, ActId.From(Act)), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentRepoUnusable.Code, refused.Error.Code);
        Assert.Empty(_r.Host.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_with_a_repo_whose_folder_is_gone_is_SND_AGENT_012_and_names_the_folder()
    {
        _r.Checks.Folders.Clear();

        var refused = await _r.Service().Handle(Request(), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentRepoUnusable.Code, refused.Error.Code);
        Assert.Contains(StartRig.RepoPath, refused.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_r.Host.Started);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, true, "not installed")]
    [InlineData(true, false, "not signed in")]
    public async Task Start_with_an_agent_not_ready_is_SND_AGENT_013_and_carries_the_sign_in_command(bool installed, bool signedIn, string said)
    {
        _r.Checks.Installed = installed;
        _r.Checks.SignedIn = signedIn;

        var refused = await _r.Service().Handle(Request(), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentNotReady.Code, refused.Error.Code);
        Assert.Contains(said, refused.Error.Message, StringComparison.Ordinal);
        Assert.Contains("claude login", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal("claude login", refused.Error.Extensions!.Single(e => e.Key == "command").Value.GetString());
        Assert.Empty(_r.Host.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_when_the_worktree_cannot_be_made_is_SND_AGENT_014_with_gits_line_and_starts_no_session()
    {
        _r.Worktrees.MakeFails = Kernel.KernelErrors.Unavailable.With("fatal: 'oneoff' is already checked out");

        var failed = await _r.Service().Handle(Request(worktree: true), Token);

        Assert.True(failed.IsError);
        Assert.Equal(SindriErrors.AgentStartFailed.Code, failed.Error.Code);
        Assert.Contains("already checked out", failed.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_r.Host.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_when_tmux_is_not_installed_is_SND_AGENT_014_and_carries_the_install_command_and_makes_no_worktree()
    {
        _r.Host.TmuxMissing = true;

        var failed = await _r.Service().Handle(Request(), Token);

        Assert.True(failed.IsError);
        Assert.Equal(SindriErrors.AgentStartFailed.Code, failed.Error.Code);
        Assert.Contains("tmux is not installed", failed.Error.Message, StringComparison.Ordinal);
        Assert.Contains("install tmux", failed.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_r.Worktrees.Names);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_when_tmux_fails_removes_the_worktree_it_made()
    {
        _r.Host.FailNextStart(SindriErrors.LaunchTimedOut.With("tmux did not start the session within 4 s"));

        var failed = await _r.Service().Handle(Request(), Token);

        Assert.True(failed.IsError);
        Assert.Equal(SindriErrors.AgentStartFailed.Code, failed.Error.Code);
        Assert.Empty(_r.Worktrees.Live);
        Assert.Equal(0, _r.Host.LiveCount);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_when_the_pane_cannot_be_found_removes_the_session_and_the_worktree()
    {
        _r.Host.PaneLookupFails = true;

        var failed = await _r.Service().Handle(Request(), Token);

        Assert.True(failed.IsError);
        Assert.Equal(SindriErrors.AgentStartFailed.Code, failed.Error.Code);
        Assert.Empty(_r.Worktrees.Live);
        Assert.Equal(0, _r.Host.LiveCount);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_with_a_prompt_over_8000_characters_is_SND_AGENT_017()
    {
        var refused = await _r.Service().Handle(Request(prompt: new string('x', 8001)), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentPromptTooLong.Code, refused.Error.Code);
        Assert.Empty(_r.Host.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_takes_a_prompt_of_exactly_8000_characters()
    {
        var started = await _r.Service().Handle(Request(prompt: new string('x', 8000)), Token);

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_takes_no_prompt()
    {
        var started = await _r.Service().Handle(Request(prompt: null), Token);

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("short")]
    [InlineData("UPPERCASE1")]
    [InlineData("has space1")]
    [InlineData("under_score1")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")]
    public async Task Start_with_an_actId_outside_the_shape_is_a_plain_400_with_no_agent_code(string actId)
    {
        var refused = await _r.Service().Handle(Request(actId: actId), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, refused.Error.Code);
        Assert.DoesNotContain("SND-AGENT", refused.Error.Code, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_replayed_actId_after_a_2xx_answers_the_first_result_and_starts_nothing()
    {
        var service = _r.Service();
        var first = await service.Handle(Request(), Token);
        var replay = await service.Handle(Request(), Token);

        Assert.True(first.IsSuccess && replay.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        _ = Assert.Single(_r.Host.Started);
        _ = Assert.Single(_r.Worktrees.Names);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refusal_stores_nothing_so_the_retry_with_the_same_actId_starts_the_session()
    {
        _r.Worktrees.MakeFails = Kernel.KernelErrors.Unavailable.With("git is busy");
        var service = _r.Service();

        var refused = await service.Handle(Request(), Token);
        var retried = await service.Handle(Request(), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentStartFailed.Code, refused.Error.Code);
        Assert.True(retried.IsSuccess, retried.IsError ? retried.Error.ToString() : null);
        _ = Assert.Single(_r.Host.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_2xx_start_appends_one_agent_op_event_keyed_by_the_actId_with_the_worktree_path()
    {
        var started = await _r.Service().Handle(Request(), Token);
        _ = await _r.Service().Handle(Request(), Token);

        var recorded = Assert.Single(_r.Base.Store.Events, e => string.Equals(e.Envelope.Type, EventTypes.AgentOp, StringComparison.Ordinal));
        var payload = _r.Base.Events.Payload<AgentOpRecorded>(recorded).Value;
        Assert.Equal(("agent.op:" + Act, "sindri-" + Act, "start", started.Value.Host.Pane), (recorded.IdempotencyKey, payload.RowId.Value, payload.Op, payload.Outcome));
        Assert.Equal("/wt/demo/" + Assert.Single(_r.Worktrees.Names), payload.Worktree);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_2xx_start_remembers_the_repo_and_agent_and_a_refusal_does_not()
    {
        _ = await _r.Service().Handle(Request(prompt: new string('x', 8001)), Token);
        Assert.Empty(_r.Choices.Remembered);

        _ = await _r.Service().Handle(Request(agent: _codex), Token);

        Assert.Equal([("demo", "codex")], _r.Choices.Remembered);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_caller_is_refused_with_SND_AUTH_002_and_nothing_starts()
    {
        using var session = _r.AsSession();

        var refused = await _r.Service().Handle(Request(), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Assert.Empty(_r.Host.Started);
        Assert.Empty(_r.Worktrees.Names);
    }
}
