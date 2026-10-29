using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>A sandboxed Step hands its scope limits to 'session wrap' (#1553): without them LimitWatchdog never runs and an OOM kill ends the session as 'exit', not 'limit'.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxWrapLimitsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-wrap-limits-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task A_Step_launch_writes_its_scope_limits_beside_the_plan_for_session_wrap()
    {
        var sindriHome = Path.Combine(_dir, "sindri-home");
        var runArea = Directory.CreateDirectory(Path.Combine(sindriHome, "workflow", "areas", "run-1")).FullName;   // the plan refuses a run area outside the Sindri home
        var runDirectory = Directory.CreateDirectory(Path.Combine(sindriHome, "workflow", "runs", "1")).FullName;
        var secrets = Directory.CreateDirectory(Path.Combine(_dir, "secrets")).FullName;
        var request = SandboxRequests.WriteScope with
        {
            Host = SandboxRequests.WriteScope.Host with { SindriHome = sindriHome },
            Paths = SandboxRequests.WriteScope.Paths with { RunDirectory = runDirectory, RunArea = runArea },
            Settings = new SandboxSettings(Limits: new SandboxLimits(Memory: "512MiB")),
        };
        await File.WriteAllTextAsync(LaunchSecrets.SecretPathFor(secrets, request.Launch), "secret", TestContext.Current.CancellationToken);
        var control = new ControlEndpoint(new Uri("http://127.0.0.1:7480"), Environment.ProcessId, Epoch: 0);
        var launch = new SandboxLaunch(
            new PassThroughSandbox(), new FileSessionCap(Path.Combine(_dir, "sessions.lock"), new FakeSliceScopes()), new FixedStages(request),
            new SandboxLaunchOptions("systemd-run", new BoxFacts(32L * 1024 * 1024 * 1024, 8), sindriHome, secrets, () => control));

        var prepared = await launch.Prepare(A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent"))).Build(), ["claude"], chosenSession: null, TestContext.Current.CancellationToken);

        Assert.True(prepared.IsSuccess, prepared.IsError ? prepared.Error.Message : null);
        var file = Path.Combine(prepared.Value.RunArea, "wrap-limits.json");
        Assert.True(File.Exists(file), $"{file} was not written");
        var read = WrapLimits.Read(file);
        Assert.True(read.IsSuccess);
        Assert.Equal(SystemdScopeArgv.UnitFor(request.Launch), read.Value.Unit);
        Assert.Equal(512L * 1024 * 1024, read.Value.MemoryMaxBytes);
        _ = Assert.NotNull(read.Value.WallSeconds);
        Assert.True(read.Value.ParkMaxSeconds > 0);
    }

    [Fact]
    public async Task A_sandboxed_launch_passes_the_limits_file_to_session_wrap()
    {
        var work = Directory.CreateDirectory(Path.Combine(_dir, "work")).FullName;
        var host = new FakeSessionHost();
        var launcher = new Launcher(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => host, new FakeTerminalViewer(), new LauncherOptions(Path.Combine(_dir, "home")) { SindriHome = "/real/sindri-home" }, new FakeTimeProvider(KnownInstants.Epoch))
        {
            Sandbox = new FakeSandboxLaunch(),
            ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
        });
        var request = A.LaunchRequest().InDirectory(work).Unattended(new StepArgs(Model: null, Policy: null, Ref: new StepRef("build", "agent"))).Build();

        _ = await launcher.Launch(request, CancellationToken.None);

        var argv = host.Started[0].Argv.ToList();
        var launchAt = argv.FindIndex(a => string.Equals(a, "--launch", StringComparison.Ordinal));
        var limitsAt = argv.FindIndex(a => string.Equals(a, "--limits", StringComparison.Ordinal));
        var dashAt = argv.FindIndex(a => string.Equals(a, "--", StringComparison.Ordinal));
        Assert.True(limitsAt > launchAt + 1, "no --limits after --launch <id>: " + string.Join(' ', argv));
        Assert.True(limitsAt + 1 < dashAt, "--limits <file> is not before the first --");
        Assert.Equal("/run/area/wrap-limits.json", argv[limitsAt + 1]);
    }

    private sealed class FixedStages(SandboxRequest request) : ISandboxLaunchStages
    {
        public ValueTask<Result<SandboxRequest>> Request(LaunchRequest launch, SessionId? chosenSession, CancellationToken cancellationToken) => ValueTask.FromResult<Result<SandboxRequest>>(request);

        public ValueTask<Result<PolicyFiles>> WritePolicy(SandboxRequest request, SandboxPlan plan, string sessionHome, CancellationToken cancellationToken) => ValueTask.FromResult<Result<PolicyFiles>>(PolicyFiles.None);

        public ValueTask<Result<Unit>> StartEgress(SandboxPlan plan, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);

        public ValueTask Release(LaunchId launch, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
