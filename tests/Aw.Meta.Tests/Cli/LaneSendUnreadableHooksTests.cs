using System.Text.Json;
using Aw.Kernel.Data;
using Aw.Meta.Infrastructure.Sqlite;

namespace Aw.Meta.Tests.Cli;

/// <summary>lane-send end to end when the message is stored but the lane's hook state can't be read: attention, exit 1, never a tool error (#4018).</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LaneSendUnreadableHooksTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private async Task Ready()
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "meta,api");
        _ = await _cli.Run("role-set", "--session", "s-meta", "--name", "t1-meta");
        var sessions = _cli.File("meta/teams/t1/lane-sessions");
        _ = Directory.CreateDirectory(sessions);
        await File.WriteAllTextAsync(Path.Combine(sessions, "api"), "sess-api\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(sessions, "api.agent"),
            JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal) { ["agent"] = "codex", ["processName"] = "codex", ["hooks"] = true }),
            TestContext.Current.CancellationToken);
    }

    // The lane hook beats live in the kv table: with it renamed away, the beat read fails after the message is stored and recorded.
    private async Task BreakHookState()
    {
        var connections = new SqliteConnectionFactory(MetaDatabase.OptionsFor(_cli.File("meta/teams/t1")));
        await using var connection = (await connections.Open(TestContext.Current.CancellationToken)).Value;
        await using var rename = connection.CreateCommand();
        rename.CommandText = "ALTER TABLE kv RENAME TO kv_gone";
        _ = await rename.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_send_stored_for_a_lane_whose_hook_state_cant_be_read_is_attention_exit_1()
    {
        await Ready();
        await BreakHookState();

        var sent = await _cli.Run("lane-send", "--lane", "api", "--text", "please rebase");

        Assert.Equal(1, sent.Exit);
        Assert.StartsWith("sent lane-send:api:1 to lane api; not woken", sent.Out, StringComparison.Ordinal);
        Assert.Contains("attention: lane api is unreadable: its hook state could not be read", sent.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("AW-META-", sent.Err, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
