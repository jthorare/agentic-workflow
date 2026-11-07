using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>
/// #2163: a sandboxed Step must see AW_SINDRI_LAUNCH_SECRET, or `sindri workflow complete` inside it is refused (SND-AUTH-002). The plan leaves the variable to the launcher, and bubblewrap
/// gets launcher values only through <see cref="ILaunchSecrets"/> (the 0600 file on descriptor 3, `--args 3`). Nothing fills the real <see cref="LaunchSecretRegistry"/>, and the other sandbox tests
/// fill it by hand or use a pass-through sandbox, so none noticed. Here the real Launcher drives the real <see cref="SandboxLaunch"/> over the real <see cref="BubblewrapSandbox"/> Wrap and the
/// registry as the product registers it; only the probe, the self-check (they run bwrap) and the host-side mounts are replaced.
/// </summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxLaunchSecretTests : IDisposable
{
    private const string SecretVariable = "AW_SINDRI_LAUNCH_SECRET";

    private readonly string _dir = Directory.CreateTempSubdirectory("aw-launch-secret-").FullName;
    private readonly ServiceProvider _provider;

    public SandboxLaunchSecretTests()
    {
        _provider = new ServiceCollection()
            .AddSingleton(new SindriSettings(Path.Combine(_dir, "sindri-app"), Path.Combine(_dir, "sindri-app", "profile.yaml")))
            .AddSingleton<TimeProvider>(new FakeTimeProvider(KnownInstants.Epoch))
            .AddSindriBubblewrapSandbox(static name => string.Equals(name, "HOME", StringComparison.Ordinal) ? "/home/owner" : null)   // the registration the product makes
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private string SindriHome => Path.Combine(_dir, "sindri-home");

    private string LauncherHome => Path.Combine(_dir, "launcher-home");

    private LaunchSecretRegistry Registry => _provider.GetRequiredService<LaunchSecretRegistry>();

    private SandboxRequest Request()
    {
        var runArea = Directory.CreateDirectory(Path.Combine(SindriHome, "workflow", "areas", "run-1")).FullName;   // the plan refuses a run area outside the Sindri home
        var runDirectory = Directory.CreateDirectory(Path.Combine(SindriHome, "workflow", "runs", "1")).FullName;
        return SandboxRequests.WriteScope with
        {
            Host = SandboxRequests.WriteScope.Host with { SindriHome = SindriHome },
            Paths = SandboxRequests.WriteScope.Paths with { RunDirectory = runDirectory, RunArea = runArea },
        };
    }

    private SandboxLaunch MakeLaunch(bool hideLaunchDirectory)
    {
        var control = new ControlEndpoint(new Uri("http://127.0.0.1:7480"), Environment.ProcessId, Epoch: 0);
        return new SandboxLaunch(
            new HostlessBox(_provider.GetRequiredService<ISandbox>(), Path.Combine(SindriHome, "launch"), hideLaunchDirectory),
            new FileSessionCap(Path.Combine(_dir, "sessions.lock"), new FakeSliceScopes()),
            new FixedStages(Request()),
            new SandboxLaunchOptions("systemd-run", new BoxFacts(32L * 1024 * 1024 * 1024, 8), SindriHome, new LauncherOptions(LauncherHome).SecretsDirectory, () => control));
    }

    [Fact]
    public async Task A_sandboxed_Step_gets_its_launch_secret_on_descriptor_3_and_never_in_an_argument()
    {
        var work = Directory.CreateDirectory(Path.Combine(_dir, "work")).FullName;
        var host = new FakeSessionHost();
        var launcher = new Launcher(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => host, new FakeTerminalViewer(), new LauncherOptions(LauncherHome) { SindriHome = SindriHome }, new FakeTimeProvider(KnownInstants.Epoch))
        {
            Sandbox = MakeLaunch(hideLaunchDirectory: false),
            ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
        });
        var request = A.LaunchRequest().InDirectory(work).Unattended(new StepArgs(Model: null, Policy: null, Ref: new StepRef("build", "agent"))).Build();

        var launched = await launcher.Launch(request, TestContext.Current.CancellationToken);

        Assert.True(launched is StartedLaunch, launched is FailedLaunch failed ? failed.Error.Message : "the launch did not start");
        var started = host.Started[0];
        var argv = started.Argv.ToList();
        var args = argv.IndexOf("--args");
        Assert.True(args >= 0 && args + 1 < argv.Count && string.Equals(argv[args + 1], "3", StringComparison.Ordinal), "the wrapped argv has no '--args 3': bubblewrap gets no launcher values. argv: " + string.Join(' ', argv));
        var secret = (await File.ReadAllTextAsync(started.Env["AW_SINDRI_LAUNCH_SECRET_FILE"], TestContext.Current.CancellationToken)).Trim();   // the secret the launcher minted, as the session home holds it
        Assert.NotEmpty(secret);
        var wrapper = argv.FindIndex(a => string.Equals(a, "sh", StringComparison.Ordinal));
        var secretsFile = argv[wrapper + 1];
        Assert.Contains($"--setenv\0{SecretVariable}\0{secret}\0", await File.ReadAllTextAsync(secretsFile, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.DoesNotContain(argv, a => a.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_launch_refused_after_it_was_prepared_leaves_no_secret_registered_for_it()
    {
        var launch = MakeLaunch(hideLaunchDirectory: true);
        var request = A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent"))).Build();
        var hidden = Path.Combine(SindriHome, "launch", "mcp-hidden.json");   // a path the plan's mask hides: refused before the agent starts (#2151)
        await File.WriteAllTextAsync(LaunchSecrets.SecretPathFor(new LauncherOptions(LauncherHome).SecretsDirectory, request.Launch), "secret", TestContext.Current.CancellationToken);

        var prepared = await launch.Prepare(request, ["claude", "--mcp-config", hidden], chosenSession: null, TestContext.Current.CancellationToken);

        Assert.True(prepared.IsError);
        Assert.Equal("SND-LAUNCH-015", prepared.Error.Code);
        Assert.Empty(Registry.For(request.Launch));
    }

    /// <summary>The real bubblewrap Wrap over a plan whose host side is the temp run area (no probe, no self-check: they run bwrap).</summary>
    private sealed class HostlessBox(ISandbox real, string launchDirectory, bool hideLaunchDirectory) : ISandbox
    {
        public Result<SandboxCapabilities> Probe() => new SandboxCapabilities(SandboxMechanism.Bubblewrap);

        public Result<SandboxPlan> Plan(SandboxRequest request)
        {
            var planned = real.Plan(request);
            if (planned.IsError)
            {
                return planned.Error;
            }

            var plan = planned.Value;
            var area = Path.GetDirectoryName(plan.Environment.First(e => string.Equals(e.Name, "AW_SANDBOX_PLAN", StringComparison.Ordinal)).Value);
            List<PlanMount> mounts =
            [
                .. plan.Mounts.Where(m => m.Kind is PlanMountKind.Dev or PlanMountKind.Proc || string.Equals(m.Path, "/", StringComparison.Ordinal)),
                new PlanMount(plan.RunDirectory, PlanMountKind.ReadWrite, Reason: "test"),
                new PlanMount("/tmp", PlanMountKind.ReadWrite, area + "/tmp", Reason: "test"),
            ];
            if (hideLaunchDirectory)
            {
                mounts.Add(new PlanMount(launchDirectory, PlanMountKind.Deny, Reason: "test"));
            }

            return plan with { Worktree = null, Mounts = mounts };
        }

        public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct) => Task.FromResult<Result<Unit>>(Unit.Value);

        public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv) => real.Wrap(plan, argv);

        public void Discard(SandboxPlan plan) => real.Discard(plan);
    }

    private sealed class FixedStages(SandboxRequest request) : ISandboxLaunchStages
    {
        public ValueTask<Result<SandboxRequest>> Request(LaunchRequest launch, SessionId? chosenSession, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<SandboxRequest>>(request with { Launch = launch.Launch });   // the plan is for the launch the launcher is preparing

        public ValueTask<Result<PolicyFiles>> WritePolicy(SandboxRequest request, SandboxPlan plan, string sessionHome, CancellationToken cancellationToken) => ValueTask.FromResult<Result<PolicyFiles>>(PolicyFiles.None);

        public ValueTask<Result<Unit>> StartEgress(SandboxPlan plan, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);

        public ValueTask Release(LaunchId launch, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
