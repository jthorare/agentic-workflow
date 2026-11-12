using Aw.Sindri.Core;
using Aw.Sindri.Features.RemoveSessionWorktree;

namespace Aw.Sindri.Tests.RemoveSessionWorktree;

/// <summary>
/// RemoveSessionWorktree (agents spec §15.8, A7; red): 'git worktree remove' without --force on the path the start recorded in its agent.op event.
/// A tree with changes, or one no start made, is SND-AGENT-018. Step 2 gives the service its ports and these tests their arrangement.
/// </summary>
public sealed class RemoveSessionWorktreeTests
{
    private const string ActId = "k3j9x0a1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RemoveSessionWorktreeService Service() => new();

    [Fact, Trait("Lane", "unit")]
    public async Task Remove_takes_a_clean_worktree_of_a_started_session_and_answers_its_path()
    {
        var removed = await Service().Handle(new RemoveSessionWorktreeRequest(ActId), Token);

        Assert.True(removed.IsSuccess, removed.IsError ? removed.Error.ToString() : null);
        Assert.Equal(ActId, removed.Value.ActId);
        Assert.False(string.IsNullOrEmpty(removed.Value.Path));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Remove_of_a_tree_with_changes_is_SND_AGENT_018_with_the_status_summary_and_keeps_the_tree()
    {
        var kept = await Service().Handle(new RemoveSessionWorktreeRequest(ActId), Token);

        Assert.True(kept.IsError);
        Assert.Equal(SindriErrors.AgentWorktreeKept.Code, kept.Error.Code);
        Assert.Contains("M ", kept.Error.Message, StringComparison.Ordinal);   // the git status line
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Remove_of_an_actId_no_start_recorded_is_SND_AGENT_018_and_removes_nothing()
    {
        var kept = await Service().Handle(new RemoveSessionWorktreeRequest("never0000"), Token);

        Assert.True(kept.IsError);
        Assert.Equal(SindriErrors.AgentWorktreeKept.Code, kept.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Remove_never_passes_force_to_git()
    {
        _ = await Service().Handle(new RemoveSessionWorktreeRequest(ActId), Token);

        // step 2: the fake git runner saw 'worktree remove <path>' and no --force
        Assert.Fail("red: arranged in step 2");
    }
}
