using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The composed launcher (API P10a, U7): stage bounds, SL8, the per-launch files, the host choice.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _host = new();
    private readonly FakeTerminalViewer _viewer = new();

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private LauncherOptions Options => new(_home);

    private Launcher Make(Func<bool, ISessionHost>? hostFor = null, ITerminalViewer? viewer = null, params AgentEntry[] entries) =>
        new(new LauncherParts(new AgentCatalog(entries.Length == 0 ? [AgentEntries.ClaudeCode, AgentEntries.Cursor] : entries), hostFor ?? (_ => _host), viewer ?? _viewer, Options, _clock) { ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null) });

    private static StepArgs Step(StepPolicy? policy = null) => new(Model: null, policy, new StepRef("build", "agent"));

    private async Task<T> Drive<T>(ValueTask<T> pending)
    {
        var task = pending.AsTask();
        for (var tick = 0; !task.IsCompleted && tick < 400; tick++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            _clock.Advance(TimeSpan.FromMilliseconds(500));
        }

        return await task.WaitAsync(TimeSpan.FromSeconds(30), TimeProvider.System, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Unattended_skips_the_viewer_and_forces_tmux()
    {
        var none = new NoSessionHost();

        var result = await Make(attended => attended ? none : _host).Launch(A.LaunchRequest().Unattended(Step()).Build(), CancellationToken.None);

        Expect.True(result is StartedLaunch { Viewer.Kind: ViewerOutcomeKind.Skipped, Host.Kind: HostKind.Tmux });
        Expect.Equal(0, _viewer.Requests.Count);
        Expect.Equal(1, _host.Started.Count);
    }

    [Fact]
    public async Task Host_past_4s_is_Failed_009_stage_host_and_kills_the_session()
    {
        var overran = new OverranHost();

        var result = await Make(_ => overran).Launch(A.LaunchRequest().Build(), CancellationToken.None);

        Expect.True(result is FailedLaunch { Stage: LaunchStage.Host } failed && failed.Error.Code == SindriErrors.LaunchTimedOut.Code);
        Expect.Equal(0, overran.LiveCount);
    }

    [Fact]
    public async Task Viewer_hang_is_Started_with_viewer_failed_009()
    {
        var result = await Drive(Make(viewer: new HangingViewer()).Launch(A.LaunchRequest().Build(), CancellationToken.None));

        Expect.True(result is StartedLaunch { Viewer: { Kind: ViewerOutcomeKind.Failed, Code: var code } } && string.Equals(code, "SND-LAUNCH-009", StringComparison.Ordinal));
        Expect.Equal(1, _host.LiveCount);
    }

    [Fact]
    public async Task None_host_viewer_failure_is_launch_failed()
    {
        _viewer.Script(ViewerOutcome.Failed(SindriErrors.LaunchViewerFailed.Code, "boom"));

        var result = await Make(_ => new NoSessionHost()).Launch(A.LaunchRequest().Build(), CancellationToken.None);

        Expect.True(result is FailedLaunch { Stage: LaunchStage.Host } failed && failed.Error.Code == SindriErrors.LaunchViewerFailed.Code);
        Expect.True(_viewer.Requests.Single().AttachArgv.Contains("claude", StringComparer.Ordinal));   // the viewer runs the agent itself on a none host
    }

    [Fact]
    public async Task The_secret_hash_is_a_0600_file_deleted_on_stop_and_the_secret_is_only_in_the_env()
    {
        var launcher = Make();
        var request = A.LaunchRequest().Build();

        var started = (StartedLaunch)await launcher.Launch(request, CancellationToken.None);

        var env = _host.Started.Single().Env;
        var file = Path.Combine(Options.SecretsDirectory, request.Launch.Value + ".hash");
        var secret = env["AW_SINDRI_LAUNCH_SECRET"];
        Expect.Equal("sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret))), (await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)).Trim());
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        Expect.Equal("1", env["AW_SINDRI_CHILD"]);
        Expect.Equal(request.Launch.Value, env["AW_SINDRI_LAUNCH"]);
        Expect.True(!_host.Started.Single().Argv.Any(a => a.Contains(secret, StringComparison.Ordinal)));
        Expect.True(!started.ToString().Contains(secret, StringComparison.Ordinal));

        _ = await launcher.Stop(request.Launch, CancellationToken.None);

        Expect.True(!File.Exists(file));
    }

    [Fact]
    public async Task A_Step_on_an_added_entry_with_no_argvHash_is_Failed_012()
    {
        var result = await Make(hostFor: null, viewer: null, AgentEntries.Aider).Launch(A.LaunchRequest().WithAgent(AgentId.From("aider")).Unattended(Step()).Build(), CancellationToken.None);

        Expect.True(result is FailedLaunch failed && failed.Error.Code == SindriErrors.LaunchNotApproved.Code);
        Expect.Equal(0, _host.LiveCount);
    }

    [Fact]
    public async Task A_Step_policy_file_is_written_0600_passed_as_settings_and_deleted_on_stop()
    {
        var launcher = Make();
        var request = A.LaunchRequest().Unattended(Step(new StepPolicy(["Read"], StepPermissionMode.ReadOnly, WritablePath: null))).Build();

        _ = await launcher.Launch(request, CancellationToken.None);

        var argv = _host.Started.Single().Argv;
        var path = argv[argv.ToList().IndexOf("--settings") + 1];
        Expect.True(File.Exists(path));
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));

        _ = await launcher.Stop(request.Launch, CancellationToken.None);

        Expect.True(!File.Exists(path));
    }

    [Fact]
    public async Task A_Step_policy_an_entry_cannot_express_is_Failed_013_and_starts_nothing()
    {
        var request = A.LaunchRequest().WithAgent(AgentId.From("cursor")).Unattended(Step(new StepPolicy(["Read"], StepPermissionMode.Plan, WritablePath: null))).Build();

        var result = await Make().Launch(request, CancellationToken.None);

        Expect.True(result is FailedLaunch failed && failed.Error.Code == SindriErrors.LaunchFormUnsupported.Code);
        Expect.Equal(0, _host.LiveCount);
    }

    [Fact]
    public async Task An_unknown_agent_is_Failed_003()
    {
        var result = await Make().Launch(A.LaunchRequest().WithAgent(AgentId.From("ghost")).Build(), CancellationToken.None);

        Expect.True(result is FailedLaunch failed && failed.Error.Code == SindriErrors.LaunchAgentMissing.Code);
    }

    [Fact]
    public async Task Probe_reports_the_terminal_the_host_and_one_row_per_catalog_entry()
    {
        var probed = await Make(hostFor: null, viewer: null, AgentEntries.ClaudeCode, AgentEntries.Aider).Probe(CancellationToken.None);

        Expect.Equal("exec-template", probed.Terminal.Kind);
        Expect.Equal(HostKind.Tmux, probed.Host.Kind);
        Assert.Equal(["claude-code", "aider"], probed.Targets.Select(t => t.Id.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_pre_chosen_session_id_is_carried_on_the_started_launch()
    {
        var result = await Make().Launch(A.LaunchRequest().Build(), CancellationToken.None);

        var started = Assert.IsType<StartedLaunch>(result);
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", started.ChosenSession);
        Assert.Contains(started.ChosenSession, _host.Started.Single().Argv, StringComparer.Ordinal);   // the id the agent was told is the id recorded
    }

    [Fact]
    public async Task A_reported_session_id_is_not_known_at_start()
    {
        var result = await Make(hostFor: null, viewer: null, AgentEntries.Codex).Launch(A.LaunchRequest().WithAgent(AgentId.From("codex")).Build(), CancellationToken.None);

        Assert.Null(Assert.IsType<StartedLaunch>(result).ChosenSession);
    }

    [Fact]
    public async Task A_reported_entry_gets_a_0600_capture_rule_that_stop_deletes()
    {
        var codex = AgentEntries.Codex with { IdCapture = "rollout-(.*)\\.jsonl$", ReportedNewestFile = "~/.codex/sessions/*/*/*/rollout-*.jsonl" };
        var launcher = Make(hostFor: null, viewer: null, codex);
        var request = A.LaunchRequest().WithAgent(codex.Id).Build();

        _ = await launcher.Launch(request, CancellationToken.None);

        var file = Path.Combine(Options.LaunchesDirectory, LaunchFiles.CaptureName(request.Launch));
        var rule = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)).RootElement;
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        Expect.Equal((codex.SessionIdPattern, codex.ReportedNewestFile, codex.IdCapture), (rule.GetProperty("sessionIdPattern").GetString(), rule.GetProperty("newestFile").GetString(), rule.GetProperty("capture").GetString()));
        Expect.True(!(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)).Contains(_host.Started.Single().Env["AW_SINDRI_LAUNCH_SECRET"], StringComparison.Ordinal));

        _ = await launcher.Stop(request.Launch, CancellationToken.None);

        Expect.True(!File.Exists(file));
    }

    [Fact]
    public async Task A_pre_chosen_entry_gets_no_capture_rule()
    {
        var request = A.LaunchRequest().Build();

        _ = await Make().Launch(request, CancellationToken.None);

        Expect.True(!File.Exists(Path.Combine(Options.LaunchesDirectory, LaunchFiles.CaptureName(request.Launch))));
    }
}
