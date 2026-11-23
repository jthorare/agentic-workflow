using System.Text.Json;
using Aw.Meta.Core.Ports;
using Aw.Meta.Tests.Delegate;
using Microsoft.Extensions.DependencyInjection;

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

    // The real store fails only on lock, disk or corruption errors, never on purpose: wrap it so the lane hook beat reads fail
    // ("lane.<lane>.beat"), while the inbox send and the outbound record before them still write.
    private static void FailBeatReads(IServiceCollection services)
    {
        var store = services.Single(static d => d.ServiceType == typeof(IKeyValueStore));
        var inner = store.ImplementationType ?? throw new InvalidOperationException("IKeyValueStore is no longer registered by type: update this test's decorator");
        _ = services.Remove(store);
        _ = services.AddScoped<IKeyValueStore>(sp => new FailingBeatValues((IKeyValueStore)ActivatorUtilities.CreateInstance(sp, inner)));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_send_stored_for_a_lane_whose_hook_state_cant_be_read_is_attention_exit_1()
    {
        await Ready();
        _cli.Services = FailBeatReads;

        var sent = await _cli.Run("lane-send", "--lane", "api", "--text", "please rebase");

        Assert.Equal(1, sent.Exit);
        Assert.StartsWith("sent lane-send:api:1 to lane api; not woken", sent.Out, StringComparison.Ordinal);
        Assert.Contains("attention: lane api is unreadable: its hook state could not be read (AW-META-012: beat store unreadable)", sent.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("AW-META-", sent.Err, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
