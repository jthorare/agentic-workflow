using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.RemoveSessionWorktree;
using Aw.Sindri.Features.StartSession;
using Aw.Sindri.Tests.StartSession;

namespace Aw.Sindri.Tests.RemoveSessionWorktree;

/// <summary>
/// RemoveSessionWorktree (agents spec §15.8, A7): 'git worktree remove' without --force on the path the start recorded in its agent.op event.
/// A tree with changes, or one no start made, is SND-AGENT-018.
/// </summary>
public sealed class RemoveSessionWorktreeTests
{
    private const string ActId = "k3j9x0a1";

    private readonly StartRig _r = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<StartedSession> Started(bool worktree = true) =>
        (await _r.Service().Handle(new StartSessionRequest(RepoName.From("demo"), AgentId.From("claude-code"), "hi", worktree, ActId), Token)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task Remove_takes_a_clean_worktree_of_a_started_session_and_answers_its_path()
    {
        _ = await Started();

        var removed = await _r.RemoveService().Handle(new RemoveSessionWorktreeRequest(ActId), Token);

        Assert.True(removed.IsSuccess, removed.IsError ? removed.Error.ToString() : null);
        Assert.Equal(ActId, removed.Value.ActId);
        Assert.Equal(_r.Worktrees.Removed, [removed.Value.Path]);
        Assert.Empty(_r.Worktrees.Live);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Remove_of_a_tree_with_changes_is_SND_AGENT_018_with_the_status_summary_and_keeps_the_tree()
    {
        _ = await Started();
        _r.Worktrees.Status = " M src/Checkout.cs";

        var kept = await _r.RemoveService().Handle(new RemoveSessionWorktreeRequest(ActId), Token);

        Assert.True(kept.IsError);
        Assert.Equal(SindriErrors.AgentWorktreeKept.Code, kept.Error.Code);
        Assert.Contains("M src/Checkout.cs", kept.Error.Message, StringComparison.Ordinal);
        Assert.Single(_r.Worktrees.Live);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Remove_of_an_actId_no_start_recorded_is_SND_AGENT_018_and_removes_nothing()
    {
        var kept = await _r.RemoveService().Handle(new RemoveSessionWorktreeRequest("never0000"), Token);

        Assert.True(kept.IsError);
        Assert.Equal(SindriErrors.AgentWorktreeKept.Code, kept.Error.Code);
        Assert.Empty(_r.Worktrees.Removed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Remove_of_a_start_that_made_no_worktree_is_SND_AGENT_018()
    {
        _ = await Started(worktree: false);

        var kept = await _r.RemoveService().Handle(new RemoveSessionWorktreeRequest(ActId), Token);

        Assert.True(kept.IsError);
        Assert.Equal(SindriErrors.AgentWorktreeKept.Code, kept.Error.Code);
        Assert.Empty(_r.Worktrees.Removed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Remove_finds_the_path_in_the_ledger_after_a_restart()
    {
        _ = await Started();
        var afterRestart = new StartedSessions(_r.Base.Store, _r.Base.Events);   // a new process: nothing in memory

        var removed = await new RemoveSessionWorktreeService(afterRestart, _r.Worktrees).Handle(new RemoveSessionWorktreeRequest(ActId), Token);

        Assert.True(removed.IsSuccess, removed.IsError ? removed.Error.ToString() : null);
        Assert.Single(_r.Worktrees.Removed);
    }
}
