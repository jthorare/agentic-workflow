using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.StartSession;

namespace Aw.Sindri.Tests.StartSession;

/// <summary>
/// StartSession (agents spec §15.8, A7; red): a one-off session is a tmux session named 'sindri-&lt;actId&gt;'. The refusals are SND-AGENT-012, 013, 014 and 017; a bad actId is a plain 400.
/// Only a 2xx result is stored under the actId. The red commit builds the service with no ports, so these tests fail with NotSupportedException; step 2 gives them a rig
/// (profile with a repo folder, a fake agent catalog and worktree maker, FakeSessionHost, an in-memory ledger) and the arrangement each name says.
/// </summary>
public sealed class StartSessionTests
{
    private const string ActId = "k3j9x0a1";
    private static readonly RepoName _repo = RepoName.From("demo");
    private static readonly AgentId _claude = AgentId.From("claude-code");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static StartSessionService Service() => new();

    private static StartSessionRequest Request(string? prompt = "fix the flaky test", bool worktree = true, string actId = ActId) => new(_repo, _claude, prompt, worktree, actId);

    [Fact, Trait("Lane", "unit")]
    public async Task Start_answers_starting_with_the_tmux_session_named_after_the_actId()
    {
        var started = await Service().Handle(Request(), Token);

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        Assert.Equal(("sindri-" + ActId, ActId, "starting"), (started.Value.Id, started.Value.ActId, started.Value.State));
        Assert.Equal(("sindri-launch", "sindri-" + ActId), (started.Value.Host.Socket, started.Value.Host.Session));
        Assert.Matches("^%[0-9]+$", started.Value.Host.Pane);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_starts_the_session_in_a_window_of_a_known_size_that_stays_when_the_agent_exits()
    {
        _ = await Service().Handle(Request(), Token);

        // step 2 reads FakeSessionHost.Started: the session is named sindri-<actId>, has a Size and RemainOnExit is on
        Assert.Fail("red: arranged in step 2");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_with_a_repo_not_in_the_profile_is_SND_AGENT_012()
    {
        var refused = await Service().Handle(Request(), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentRepoUnusable.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_with_a_repo_whose_folder_is_gone_is_SND_AGENT_012_and_names_the_folder()
    {
        var refused = await Service().Handle(Request(), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentRepoUnusable.Code, refused.Error.Code);
        Assert.Contains("/work/demo", refused.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_with_an_agent_not_signed_in_is_SND_AGENT_013_and_carries_the_sign_in_command()
    {
        var refused = await Service().Handle(Request(), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentNotReady.Code, refused.Error.Code);
        Assert.Contains("claude", refused.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_when_the_worktree_cannot_be_made_is_SND_AGENT_014_and_leaves_no_session_or_worktree()
    {
        var failed = await Service().Handle(Request(worktree: true), Token);

        Assert.True(failed.IsError);
        Assert.Equal(SindriErrors.AgentStartFailed.Code, failed.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_when_tmux_is_not_installed_is_SND_AGENT_014_and_carries_the_install_command()
    {
        var failed = await Service().Handle(Request(), Token);

        Assert.True(failed.IsError);
        Assert.Equal(SindriErrors.AgentStartFailed.Code, failed.Error.Code);
        Assert.Contains("install", failed.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_when_tmux_starts_the_session_but_the_worktree_was_made_removes_both_on_failure()
    {
        var failed = await Service().Handle(Request(), Token);

        Assert.True(failed.IsError);
        Assert.Equal(SindriErrors.AgentStartFailed.Code, failed.Error.Code);
        // step 2: the fake host holds no session and the worktree maker holds no path
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_with_a_prompt_over_8000_characters_is_SND_AGENT_017()
    {
        var refused = await Service().Handle(Request(prompt: new string('x', 8001)), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentPromptTooLong.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_takes_a_prompt_of_exactly_8000_characters()
    {
        var started = await Service().Handle(Request(prompt: new string('x', 8000)), Token);

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_takes_no_prompt()
    {
        var started = await Service().Handle(Request(prompt: null), Token);

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("short")]
    [InlineData("UPPERCASE1")]
    [InlineData("has space1")]
    [InlineData("under_score1")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")]
    [InlineData("")]
    public async Task Start_with_an_actId_outside_the_shape_is_a_plain_400_with_no_agent_code(string actId)
    {
        var refused = await Service().Handle(Request(actId: actId), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, refused.Error.Code);
        Assert.DoesNotContain("SND-AGENT", refused.Error.Code, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_replayed_actId_after_a_2xx_answers_the_first_result_and_starts_nothing()
    {
        var service = Service();
        var first = await service.Handle(Request(), Token);
        var replay = await service.Handle(Request(), Token);

        Assert.True(first.IsSuccess && replay.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        // step 2: FakeSessionHost.Started holds one session
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refusal_stores_nothing_so_the_retry_with_the_same_actId_starts_the_session()
    {
        var service = Service();
        var refused = await service.Handle(Request(), Token);   // step 2: worktree maker fails once
        var retried = await service.Handle(Request(), Token);

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.AgentStartFailed.Code, refused.Error.Code);
        Assert.True(retried.IsSuccess, retried.IsError ? retried.Error.ToString() : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_2xx_start_appends_one_agent_op_event_keyed_by_the_actId_with_the_worktree_path()
    {
        var started = await Service().Handle(Request(), Token);

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        // step 2: the ledger holds one agent.op keyed by the actId; its payload carries the recorded worktree path the DELETE route reads
        Assert.Fail("red: arranged in step 2");
    }
}
