using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>The wire shapes of the sandbox types and events (sandbox spec §6, §8; SB-0). Goldens under Golden/Sandbox/events.</summary>
public sealed class SandboxWireTests
{
    private static JsonSerializerOptions Json => SliceHarness.Json;

    private static SandboxSummary Summary(bool attended) => new(
        SandboxMechanism.Bubblewrap, "9f2c0a41d8e6", ["/work/web/.claude/worktrees/sindri-x-1", "/home/thor/.agentic-workflow/sindri-app/workflow/runs/x/build/1"],
        ["model", "packages"], NetworkMode.Enforce, InnerState.On, new SandboxLimitsSummary("12GiB", "400%", 2048, attended ? null : "70m"),
        new SandboxCredentialsSummary(GithubCredentials.None), ModelLoginSource.OwnerCopy, attended);

    private static void Match(string json, string name) =>
        Golden.Match(json + "\n", typeof(SandboxWireTests).Assembly, "Golden/Sandbox/events/" + name, Normalization.None);

    [Fact, Trait("Lane", "unit")]
    public void Launch_started_carries_the_sandbox_summary()
    {
        var started = new LaunchStarted(
            A.FirstLaunchId, A.FirstItemId, LaunchMode.Open, AgentId.From("claude-code"), SessionId.From("0192f0c1-a2b3"), "sindri ITM-1",
            new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Opened, Code: null, Detail: null), ["tmux", "-L", "sindri", "attach-session", "-t", "=snd-x"], CatalogSource.Builtin, "hash",
            Summary(attended: false));

        Match(JsonSerializer.Serialize(started, Json), "launch-started-sandbox.json");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_public_launch_started_carries_the_sandbox_summary_too()
    {
        var started = new LaunchStartedPublic(
            A.FirstLaunchId, A.FirstItemId, LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri ITM-1",
            new LaunchViewerPublic("print", "Print", ViewerOutcomeKind.Opened, Code: null), ["tmux", "attach"], CatalogSource.Builtin, "hash", Summary(attended: false));

        var json = JsonSerializer.Serialize(started, Json);

        Assert.Contains("\"sandbox\":{\"mechanism\":\"bubblewrap\"", json, StringComparison.Ordinal);
        Assert.Contains("\"inner\":\"on\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("auto", json, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_launch_with_a_reason_has_its_wire_shape() =>
        Match(JsonSerializer.Serialize(new LaunchFailed(A.FirstLaunchId, A.FirstItemId, "SND-LAUNCH-017", "The agent's sign-in isn't available for Steps (expired)", Stage: null, Reason: "expired", Source: "separate-login"), Json), "launch-failed-reason.json");

    [Fact, Trait("Lane", "unit")]
    public void A_failed_launch_without_a_reason_is_unchanged() =>
        Assert.DoesNotContain("reason", JsonSerializer.Serialize(new LaunchFailed(A.FirstLaunchId, A.FirstItemId, "SND-LAUNCH-003", "gone", Stage: null), Json), StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void An_overflow_denial_has_its_wire_shape() =>
        Match(JsonSerializer.Serialize(new SandboxDenied(A.FirstLaunchId, DenialKind.Path, Target: "", "overflow", 37, Overflow: true), Json), "sandbox-denied-overflow.json");

    [Fact, Trait("Lane", "unit")]
    public void A_counted_denial_has_no_overflow_field() =>
        Assert.DoesNotContain("overflow", JsonSerializer.Serialize(new SandboxDenied(A.FirstLaunchId, DenialKind.Network, "github.com", "group:github-off", 1), Json), StringComparison.OrdinalIgnoreCase);

    [Fact, Trait("Lane", "unit")]
    public void A_limit_ending_is_built_with_all_its_fields()
    {
        var ended = SessionEnded.Limited(A.FirstLaunchId, new LimitEnd(LimitKind.Wall, 4_200, 4_300, new LimitPeaks(1, 2, 3, 4_300)));

        Assert.Equal((SessionEndReason.Limit, null, LimitKind.Wall, 4_200L, 4_300L), (ended.Reason, ended.ExitCode, ended.Kind, ended.Limit, ended.Peak));
        Assert.Equal(4_300L, ended.Peaks!.WallSeconds);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_old_session_ended_payload_still_reads()
    {
        var read = JsonSerializer.Deserialize<SessionEnded>("{\"launch\":\"" + A.FirstLaunchId.Value + "\",\"reason\":\"lost\",\"exitCode\":null}", Json)!;

        Assert.Equal((SessionEndReason.Lost, null, null), (read.Reason, read.Kind, read.Limit));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_enum_member_has_the_wire_name_the_text_helpers_use()
    {
        Assert.All(Enum.GetValues<ModelLoginSource>(), v => Assert.Equal(JsonSerializer.Serialize(v, Json), $"\"{SandboxWire.Name(v)}\""));
        Assert.All(Enum.GetValues<NetworkMode>(), v => Assert.Equal(JsonSerializer.Serialize(v, Json), $"\"{SandboxWire.Name(v)}\""));
        Assert.All(Enum.GetValues<InnerSandbox>(), v => Assert.Equal(JsonSerializer.Serialize(v, Json), $"\"{SandboxWire.Name(v)}\""));
        Assert.All(Enum.GetValues<GithubCredentials>(), v => Assert.Equal(JsonSerializer.Serialize(v, Json), $"\"{SandboxWire.Name(v)}\""));
        Assert.All(Enum.GetValues<MountMode>(), v => Assert.Equal(JsonSerializer.Serialize(v, Json), $"\"{SandboxWire.Name(v)}\""));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_attended_summary_says_attended_and_has_no_wall()
    {
        var json = JsonSerializer.Serialize(Summary(attended: true), Json);

        Assert.Contains("\"attended\":true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"wall\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("attended", JsonSerializer.Serialize(Summary(attended: false), Json), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Started_without_a_sandbox_summary_serializes_as_before()
    {
        var started = new LaunchStartedPublic(
            A.FirstLaunchId, A.FirstItemId, LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri ITM-1",
            new LaunchViewerPublic("print", "Print", ViewerOutcomeKind.Opened, Code: null), ["tmux", "attach"], CatalogSource.Builtin, "hash");

        Assert.DoesNotContain("sandbox", JsonSerializer.Serialize(started, Json), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_limit_kill_names_its_kind_limit_and_peaks()
    {
        var ended = new SessionEnded(A.FirstLaunchId, SessionEndReason.Limit, ExitCode: null, LimitKind.Memory, 12_884_901_888, 12_884_901_888, new LimitPeaks(12_884_901_888, 310, 214, 1_900));

        Match(JsonSerializer.Serialize(ended, Json), "session-ended-limit.json");
    }

    [Fact, Trait("Lane", "unit")]
    public void Session_ended_exit_shape_is_unchanged()
    {
        var json = JsonSerializer.Serialize(new SessionEnded(A.FirstLaunchId, SessionEndReason.Exit, 0), Json);

        Assert.Equal("{\"launch\":\"" + A.FirstLaunchId.Value + "\",\"reason\":\"exit\",\"exitCode\":0}", json);
    }

    [Fact, Trait("Lane", "unit")]
    public void Sandbox_denied_has_its_wire_shape() =>
        Match(JsonSerializer.Serialize(new SandboxDenied(A.FirstLaunchId, DenialKind.Network, "github.com", "group:github-off", 10), Json), "sandbox-denied.json");

    [Fact, Trait("Lane", "unit")]
    public void Sandbox_limit_hit_has_its_wire_shape() =>
        Match(JsonSerializer.Serialize(new SandboxLimitHit(A.FirstLaunchId, LimitHitKind.Pids, 2048), Json), "sandbox-limit-hit.json");

    [Fact, Trait("Lane", "unit")]
    public void Sandbox_events_are_public_and_invalidate_a_view()
    {
        foreach (var type in new[] { EventTypes.SandboxDenied, EventTypes.SandboxLimitHit })
        {
            var row = Assert.Single(EventCatalog.All, e => e.Type == type);

            Assert.True(row.Public);
            Assert.NotEmpty(row.Invalidates);
            Assert.All(row.Invalidates, v => Assert.Contains(v, ApiViews.All, StringComparer.Ordinal));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_login_sources_are_ordered_and_named()
    {
        Assert.True(ModelLoginSource.OwnerCopy < ModelLoginSource.SeparateLogin);
        Assert.Equal("\"owner-copy\"", JsonSerializer.Serialize(ModelLoginSource.OwnerCopy, Json));
        Assert.Equal("\"separate-login\"", JsonSerializer.Serialize(ModelLoginSource.SeparateLogin, Json));
    }

    [Fact, Trait("Lane", "unit")]
    public void StepPolicy_login_source_is_omitted_when_null()
    {
        var plain = JsonSerializer.Serialize(new StepPolicy(["Read"], StepPermissionMode.ReadOnly, WritablePath: null), Json);
        var asking = JsonSerializer.Serialize(new StepPolicy(["Read"], StepPermissionMode.ReadOnly, WritablePath: null, LoginSource: ModelLoginSource.SeparateLogin), Json);

        Assert.DoesNotContain("loginSource", plain, StringComparison.Ordinal);
        Assert.Contains("\"loginSource\":\"separate-login\"", asking, StringComparison.Ordinal);
    }
}
