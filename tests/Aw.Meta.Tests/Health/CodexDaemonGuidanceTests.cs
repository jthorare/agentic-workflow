using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Tests.Health;

/// <summary>#2286: a Codex lane whose hooks never ran is told what is wrong with its identity, whatever the catalog id says.</summary>
public sealed class CodexDaemonGuidanceTests
{
    private const string TrustLine = "hooks never ran: trust them with /hooks in codex (each install that changes a hook asks again)";

    [Fact, Trait("Lane", "unit")]
    public void A_Codex_alias_gets_the_Codex_guidance_not_the_generic_install_advice()
    {
        var alias = new LaneAgent("codex-yolo", "codex", Hooks: true, Codex: true, NoDaemon: true);

        Assert.Equal(TrustLine, LaneHealthReader.HooksNotRunning(alias));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_alias_recorded_before_the_Codex_flag_is_known_by_its_process_name()
    {
        var alias = new LaneAgent("codex-yolo", "codex", Hooks: true);

        Assert.Equal(TrustLine, LaneHealthReader.HooksNotRunning(alias));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_Codex_started_without_no_daemon_says_the_shared_daemon_may_run_the_hooks_as_another_team()
    {
        var alias = new LaneAgent("codex-yolo", "codex", Hooks: true, Codex: true, NoDaemon: false);

        var line = LaneHealthReader.HooksNotRunning(alias);

        Assert.Equal(TrustLine + "; if they are trusted, codex's shared daemon may be running them as another team (this lane started without --no-daemon: update codex, or stop the other team's codex)", line);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_skipped_hook_on_a_Codex_started_without_no_daemon_names_the_daemon()
    {
        var codex = new LaneAgent("codex", "codex", Hooks: true, Codex: true, NoDaemon: false);
        var trace = new LaneHookTrace(new DateTimeOffset(2026, 11, 8, 5, 34, 0, TimeSpan.Zero), "meta-lane-gate", "Stop", "not-own-agent");

        var line = LaneHealthReader.HooksNotRunning(codex, trace);

        Assert.Equal("hooks ran but skipped the lane: the hook's process is not under the lane's agent (Stop at 05:34:00Z); codex's shared daemon runs hooks outside the lane (this lane started without --no-daemon)", line);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_skipped_hook_on_a_Codex_started_with_no_daemon_is_a_nested_agent_as_before()
    {
        var codex = new LaneAgent("codex", "codex", Hooks: true, Codex: true, NoDaemon: true);
        var trace = new LaneHookTrace(new DateTimeOffset(2026, 11, 8, 5, 34, 0, TimeSpan.Zero), "meta-lane-gate", "Stop", "not-own-agent");

        var line = LaneHealthReader.HooksNotRunning(codex, trace);

        Assert.Equal("hooks ran but skipped the lane: the hook's process is not under the lane's agent (Stop at 05:34:00Z)", line);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_agent_that_is_not_Codex_keeps_its_own_line() =>
        Assert.Equal("hooks never ran: check the gemini hooks are installed", LaneHealthReader.HooksNotRunning(new LaneAgent("gemini", "gemini", Hooks: true)));
}
