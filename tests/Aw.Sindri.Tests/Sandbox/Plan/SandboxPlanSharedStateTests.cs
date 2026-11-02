using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The read-write state the hooks share with the box is made, never skipped; the agent home's write set is the spike's; a transcript key Claude Code would shorten is refused.</summary>
public sealed class SandboxPlanSharedStateTests
{
    // Spike item 2, "Claude Code write set": the login files live in the copy directory, so they are not part of the set bound out of the owner's home.
    private static readonly string[] _spikeWriteSet =
        ["projects", "sessions", "session-env", "shell-snapshots", "file-history", "todos", "history.jsonl", "backups", "cache", "state", "mcp-needs-auth-cache.json", ".last-update-result.json", ".last-cleanup"];

    private static readonly string[] _ownersAlone = ["settings.json", "hooks", "skills", "keybindings.json", "CLAUDE.md", ".credentials.json", ".claude.json", "plugins"];

    [Fact, Trait("Lane", "unit")]
    public void Read_write_state_mounts_are_never_optional_and_read_only_ones_are()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        foreach (var path in new[] { "judge", "locks", "context-guard/state" })
        {
            Assert.False(plan.Mounts.Single(m => m.Path == $"{Req.StateDir}/{path}").Optional, path);
        }

        Assert.True(plan.Mounts.Single(m => m.Path == $"{Req.StateDir}/bin-r1b").Optional);
        Assert.True(plan.Mounts.Single(m => m.Path == $"{Req.Home}/.cargo").Optional);
    }

    [Fact, Trait("Lane", "unit")]
    public void Agent_home_write_set_is_exactly_the_named_one()
    {
        var home = Req.Home + "/.claude";

        Assert.All(_spikeWriteSet, name => Assert.True(AgentHomeWriteSet.Allows(home, name), name));
        Assert.All(_ownersAlone, name => Assert.False(AgentHomeWriteSet.Allows(home, name), name));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_worktree_whose_transcript_key_claude_code_would_shorten_is_refused()
    {
        var long_ = Req.Repo + "/.claude/worktrees/" + new string('w', 200);
        var request = Req.WriteScope with { Paths = Req.WriteScope.Paths with { Worktree = long_ }, Policy = Req.WriteScope.Policy! with { WritablePath = long_ } };

        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, request);

        Assert.Equal(SindriErrors.LaunchUnconfinable.Code, result.Error.Code);
        Assert.Contains("transcript", result.Error.Message, StringComparison.Ordinal);
    }
}
