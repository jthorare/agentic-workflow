using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>Where 'session wrap' finds the agent's own session id (launcher spec §4): the hook's file first, then, once the hook has had its chance, the single new file the CLI wrote.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SessionIdFilesTests : IDisposable
{
    private const string One = "0a1b2c3d-0000-4000-8000-000000000001";
    private const string Two = "0a1b2c3d-0000-4000-8000-000000000002";
    private const string Pattern = "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
    private static readonly LaunchId _other = LaunchId.From("LCH-00000000000000000000000000000002");
    private readonly string _home = Directory.CreateTempSubdirectory("aw-wrap-ids-").FullName;
    private readonly DateTimeOffset _started = RealTime.NowOffset.AddSeconds(-20);

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private string Launches => Path.Combine(_home, LaunchFiles.Directory);

    private string Sessions => Path.Combine(_home, "agent-sessions");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private void Write(string name, string text)
    {
        _ = Directory.CreateDirectory(Launches);
        File.WriteAllText(Path.Combine(Launches, name), text);
    }

    private void Rule(string? newestFile = null)
    {
        var rule = new CaptureRule(Pattern, newestFile ?? Path.Combine(Sessions, "*", "rollout-*.jsonl"), "rollout-.*-([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\\.jsonl$");
        Write(LaunchFiles.CaptureName(A.FirstLaunchId), System.Text.Encoding.UTF8.GetString(AwJson.Serialize(rule, AwJson.CreateOptions(SindriJsonContext.Default))));
    }

    private string Rollout(string id, string day = "d1", DateTimeOffset? created = null)
    {
        var dir = Path.Combine(Sessions, day);
        _ = Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"rollout-2026-10-17T10-00-00-{id}.jsonl");
        File.WriteAllText(path, "{}\n");
        File.SetCreationTimeUtc(path, (created ?? RealTime.NowOffset).UtcDateTime);
        File.SetLastWriteTimeUtc(path, (created ?? RealTime.NowOffset).UtcDateTime);
        return path;
    }

    private ValueTask<SessionIdScan> Look(TimeSpan elapsed) => new SessionIdFiles(_home).Look(A.FirstLaunchId, _started, elapsed, Token);

    [Fact]
    public async Task The_hooks_file_is_the_id_and_wrap_deletes_it_once_read()
    {
        Write(LaunchFiles.SessionIdName(A.FirstLaunchId), One + "\n");

        var scan = await Look(TimeSpan.FromSeconds(1));

        Expect.Equal((One, false, false), (scan.Id!.Value.Value, scan.Ambiguous, File.Exists(Path.Combine(Launches, LaunchFiles.SessionIdName(A.FirstLaunchId)))));
    }

    [Fact]
    public async Task An_id_the_entrys_pattern_refuses_is_dropped_with_its_file()
    {
        Rule();
        Write(LaunchFiles.SessionIdName(A.FirstLaunchId), "not-a-uuid\n");

        var scan = await Look(TimeSpan.FromSeconds(1));

        Expect.Equal((null, false), (scan.Id?.Value, File.Exists(Path.Combine(Launches, LaunchFiles.SessionIdName(A.FirstLaunchId)))));
    }

    [Fact]
    public async Task An_id_that_breaks_the_generic_bound_is_dropped()
    {
        Write(LaunchFiles.SessionIdName(A.FirstLaunchId), "../../etc/passwd\n");

        var scan = await Look(TimeSpan.FromSeconds(1));

        Expect.Equal((null, false), (scan.Id?.Value, scan.Ambiguous));
    }

    [Fact]
    public async Task The_file_fallback_waits_for_the_hooks_chance()
    {
        Rule();
        _ = Rollout(One);

        var early = await Look(TimeSpan.FromSeconds(2));
        var late = await Look(SessionIdFiles.HookGrace);

        Expect.Equal((null, One), (early.Id?.Value, late.Id?.Value));
    }

    [Fact]
    public async Task Ambiguous_fallback_records_no_id()
    {
        Rule();
        _ = Rollout(One, "d1");
        _ = Rollout(Two, "d2");

        var scan = await Look(SessionIdFiles.HookGrace);

        Expect.Equal((null, true), (scan.Id?.Value, scan.Ambiguous));
    }

    [Fact]
    public async Task A_file_older_than_the_start_or_an_id_another_launch_holds_is_not_a_candidate()
    {
        Rule();
        _ = Rollout(One, "d1", created: _started.AddMinutes(-5));
        _ = Rollout(Two, "d2");
        Write(LaunchFiles.SessionIdName(_other), Two + "\n");

        var scan = await Look(SessionIdFiles.HookGrace);

        Expect.Equal((null, false), (scan.Id?.Value, scan.Ambiguous));
    }

    [Fact]
    public async Task Nothing_to_find_is_nothing()
    {
        var scan = await Look(SessionIdFiles.HookGrace);

        Expect.Equal((null, false), (scan.Id?.Value, scan.Ambiguous));
    }
}
