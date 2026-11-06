using System.Text.Json;
using Aw.Kernel.Testing;

namespace Aw.Meta.Tests.Cli;

/// <summary>The four lane commands end to end over a real meta.db, lane-sessions directory and a stub tmux program.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LaneCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private string LaneSessions => _cli.File("meta/teams/t1/lane-sessions");

    private string TmuxLog => _cli.File("tmux.log");

    private string Transcript => _cli.File("meta.jsonl");

    private async Task Ready(bool hooks = true, string agent = "codex")
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "meta,api");
        _ = await _cli.Run("role-set", "--session", "s-meta", "--name", "t1-meta");
        _ = Directory.CreateDirectory(LaneSessions);
        var session = Path.Combine(LaneSessions, "api");
        await File.WriteAllTextAsync(session, "sess-api\n", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(session, _cli.Clock.GetUtcNow().UtcDateTime.AddMinutes(-10));   // the session id was recorded before the hooks ran
        await File.WriteAllTextAsync(
            Path.Combine(LaneSessions, "api.agent"),
            JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal) { ["agent"] = agent, ["processName"] = agent, ["hooks"] = hooks }),
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Transcript, string.Empty, TestContext.Current.CancellationToken);
        var tmux = _cli.File("tmux");
        _ = ExecutableScript.Write(
            tmux,
            $"echo \"$@\" >> '{TmuxLog}'\n[ \"$1\" = -L ] && shift 2\ncase \"$1\" in display-message) echo {agent};; esac\n");
        _cli.SetEnv("AW_TMUX", tmux);
    }

    private void AsLane(string lane = "api")
    {
        _cli.SetEnv("AW_META_TEAM", "t1");
        _cli.SetEnv("AW_META_LANE", lane);
    }

    // What the lane's own hooks export; without it a lane-inbox is a hand-typed call and records no hook beat.
    private void AsHook() => _cli.SetEnv("AW_META_HOOK", "UserPromptSubmit");

    // A lane-send that wakes the lane waits for the lane's next hook beat. The fake clock only moves when told to, so this moves it while the
    // send is pending and, once the wake's Enter is in the tmux log, lets the lane's stop hook answer (lane-stop-check records a hook beat).
    private async Task<CliResult> SendAnswered(params string[] args)
    {
        var send = _cli.Run(args);
        var answered = false;
        while (!send.IsCompleted)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
            if (!answered && File.Exists(TmuxLog) && (await File.ReadAllTextAsync(TmuxLog, TestContext.Current.CancellationToken)).Contains("Enter", StringComparison.Ordinal))
            {
                _cli.Clock.Advance(TimeSpan.FromMilliseconds(500));
                AsHook();
                _ = await _cli.Run("lane-stop-check");
                answered = true;
            }

            _cli.Clock.Advance(TimeSpan.FromMilliseconds(500));
        }

        return await send;
    }

    private Task<CliResult> MetaStop() => _cli.Run("stop-check", "--session", "s-meta", "--transcript", Transcript);

    [Fact, Trait("Lane", "integration")]
    public async Task The_whole_exchange_a_send_is_delivered_the_stop_is_gated_the_reply_is_recorded_and_the_meta_closes_it_with_lane_send()
    {
        await Ready();
        AsLane();
        AsHook();
        var first = await _cli.Run("lane-send", "--lane", "api", "--text", "please rebase");
        var inbox = await _cli.Run("lane-inbox");
        var again = await _cli.Run("lane-inbox");
        var blocked = await _cli.Run("lane-stop-check");
        var reply = await _cli.RunWithInput("rebased and pushed\n", "lane-reply", "--stdin");
        var released = await _cli.Run("lane-stop-check");
        var owed = await MetaStop();
        _cli.Clock.Advance(TimeSpan.FromMinutes(1));
        var answered = await SendAnswered("lane-send", "--lane", "api", "--text", "thanks. LANE MAP v1: api=t1-api");   // the lane map now has the Codex lane: its broadcast is owed too
        var cleared = await MetaStop();

        Assert.Equal(1, first.Exit);
        Assert.Contains("hooks never ran", first.Out, StringComparison.Ordinal);
        Assert.Equal(0, inbox.Exit);
        Assert.Contains("MESSAGE FROM THE META to lane api:", inbox.Out, StringComparison.Ordinal);
        Assert.Contains("- please rebase", inbox.Out, StringComparison.Ordinal);
        Assert.Contains("REPLY TO META:", inbox.Out, StringComparison.Ordinal);
        Assert.Equal((0, string.Empty), (again.Exit, again.Out));
        Assert.Equal(1, blocked.Exit);
        Assert.Contains("owes the meta a reply", blocked.Out, StringComparison.Ordinal);
        Assert.Equal((0, "recorded lane:api:1" + Environment.NewLine), (reply.Exit, reply.Out));
        Assert.Equal((0, string.Empty), (released.Exit, released.Out));
        Assert.Equal(1, owed.Exit);
        Assert.Contains("reply to t1-api (lane:api:1): rebased and pushed", owed.Out, StringComparison.Ordinal);
        Assert.Contains("aw-meta lane-send --lane api", owed.Out, StringComparison.Ordinal);
        Assert.Equal(0, answered.Exit);
        Assert.Equal((0, string.Empty), (cleared.Exit, cleared.Out));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_stop_cap_releases_a_lane_that_never_replies_after_AW_META_STOP_MAX_and_health_says_so()
    {
        await Ready();
        AsLane();
        AsHook();
        _cli.SetEnv("AW_META_STOP_MAX", "2");
        _ = await _cli.Run("lane-send", "--lane", "api", "--text", "answer me");
        _ = await _cli.Run("lane-inbox");

        var turn = await _cli.Run("lane-stop-check");
        var one = await _cli.Run("lane-stop-check", "--stop-hook-active");
        var two = await _cli.Run("lane-stop-check", "--stop-hook-active");
        var capped = await _cli.Run("lane-stop-check", "--stop-hook-active");
        var health = await _cli.Run("health");

        Assert.Equal([1, 1, 1, 0], [turn.Exit, one.Exit, two.Exit, capped.Exit]);
        Assert.StartsWith("aw-meta: stop allowed after 2 identical blocks", capped.Out, StringComparison.Ordinal);
        Assert.Contains("lane api (codex): obligation not delivered (stop cap)", health.Out, StringComparison.Ordinal);
        Assert.Equal(1, health.Exit);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Health_and_lanes_show_a_launch_only_lane_and_a_lane_with_hooks_running()
    {
        await Ready(hooks: false, agent: "aider");
        AsLane();

        var health = await _cli.Run("health");
        var lanes = await _cli.Run("lanes");

        Assert.Contains("lane api (aider): launch-only: no obligations", health.Out, StringComparison.Ordinal);
        Assert.Contains("api          t1-api (Direct) [aider: launch-only: no obligations]", lanes.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_idle_lane_with_its_agent_up_and_nobody_attached_is_woken_with_a_literal_send_keys()
    {
        await Ready();
        AsLane();
        AsHook();
        _ = await _cli.Run("lane-inbox");   // the lane's hook has run
        _ = await _cli.Run("lane-stop-check");   // and the lane is idle

        var sent = await SendAnswered("lane-send", "--lane", "api", "--text", "wake up");

        Assert.Equal(0, sent.Exit);
        Assert.Contains("; woken", sent.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("not woken", sent.Out, StringComparison.Ordinal);
        var log = await File.ReadAllLinesAsync(TmuxLog, TestContext.Current.CancellationToken);
        Assert.Contains(log, l => l.StartsWith("-L t1 send-keys -t =t1-api: -l ", StringComparison.Ordinal));
        Assert.Contains(log, l => string.Equals(l, "-L t1 send-keys -t =t1-api: Enter", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_error_codes_and_exit_codes_of_the_four_commands()
    {
        await Ready();
        var unsetInbox = await _cli.Run("lane-inbox");
        var unsetStop = await _cli.Run("lane-stop-check");
        var unsetReply = await _cli.Run("lane-reply", "--text", "x");
        var unknown = await _cli.Run("lane-send", "--lane", "nope", "--text", "x");
        var claude = await _cli.Run("lane-send", "--lane", "meta", "--text", "x");
        AsLane("ui");
        var unrecorded = await _cli.Run("lane-reply", "--text", "x");
        var both = await _cli.Run("lane-send", "--lane", "api", "--text", "x", "--file", "y");

        Assert.All([unsetInbox, unsetStop, unsetReply], r => Assert.StartsWith("AW-META-014:", r.Err, StringComparison.Ordinal));
        Assert.All([unsetInbox, unsetStop, unsetReply, unknown, claude, unrecorded, both], r => Assert.Equal(2, r.Exit));
        Assert.StartsWith("AW-META-016:", unknown.Err, StringComparison.Ordinal);
        Assert.StartsWith("AW-META-016:", claude.Err, StringComparison.Ordinal);
        Assert.StartsWith("AW-META-015:", unrecorded.Err, StringComparison.Ordinal);
        Assert.StartsWith("AW-META-001:", both.Err, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_lane_recorded_with_the_claude_agent_is_AW_META_017()
    {
        await Ready(agent: "claude-code");

        var sent = await _cli.Run("lane-send", "--lane", "api", "--text", "x");

        Assert.Equal(2, sent.Exit);
        Assert.StartsWith("AW-META-017:", sent.Err, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_lane_hooks_commands_pass_silently_when_no_team_is_set_up_and_lane_send_is_AW_META_002()
    {
        AsLane();

        var inbox = await _cli.Run("lane-inbox");
        var stop = await _cli.Run("lane-stop-check");
        var reply = await _cli.Run("lane-reply", "--text", "x");
        var sent = await _cli.Run("lane-send", "--lane", "api", "--text", "x");

        Assert.All([inbox, stop, reply], r => Assert.Equal((0, string.Empty, string.Empty), (r.Exit, r.Out, r.Err)));
        Assert.StartsWith("AW-META-002:", sent.Err, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Json_output_names_the_lane_and_the_id()
    {
        await Ready();
        AsLane();
        AsHook();
        _ = await _cli.Run("lane-inbox");
        _ = await _cli.Run("lane-stop-check");

        var sent = await SendAnswered("lane-send", "--lane", "api", "--text", "hi", "--json");

        using var document = JsonDocument.Parse(sent.Out);
        Assert.Equal("api", document.RootElement.GetProperty("lane").GetString());
        Assert.Equal("lane-send:api:1", document.RootElement.GetProperty("id").GetString());
        Assert.True(document.RootElement.GetProperty("woken").GetBoolean());
    }

    public void Dispose() => _cli.Dispose();
}
