using System.Text.Json;
using Aw.Kernel.Testing;

namespace Aw.Meta.Tests.Cli;

/// <summary>A lane's stop block previews a message the lane was already shown and prints one it has not seen in full (#2498).</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LaneStopPreviewTests : IDisposable
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

    [Fact, Trait("Lane", "integration")]
    public async Task A_block_shows_a_delivered_message_as_its_80_character_preview()
    {
        await Ready();
        AsLane();
        AsHook();
        var text = "please " + new string('x', 193);
        _ = await _cli.Run("lane-send", "--lane", "api", "--text", text);
        _ = await _cli.Run("lane-inbox");

        var blocked = await _cli.Run("lane-stop-check");

        Assert.Equal(1, blocked.Exit);
        Assert.Contains("- " + text[..80] + "...", blocked.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(text, blocked.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_block_prints_an_undelivered_message_in_full()
    {
        await Ready();
        AsLane();
        AsHook();
        var text = "please " + new string('x', 193);
        _ = await _cli.Run("lane-send", "--lane", "api", "--text", text);

        var blocked = await _cli.Run("lane-stop-check");

        Assert.Equal(1, blocked.Exit);
        Assert.Contains(text, blocked.Out, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
