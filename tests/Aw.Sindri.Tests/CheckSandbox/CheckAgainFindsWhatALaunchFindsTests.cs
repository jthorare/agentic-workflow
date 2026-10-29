using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Features;
using Aw.Sindri.Features.CheckSandbox;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.CheckSandbox;

/// <summary>
/// Issue #1554: Check again has to find what a real launch finds, and each blocker has to say what to fix. Check again plans a fixture and self-checks it without the session home
/// a launch makes first (so bubblewrap fails on a missing bind source and blames fs-deny), the form check flags every installed agent instead of the one Steps run, and a blocker
/// drops the refusal's detail.
/// </summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class CheckAgainFindsWhatALaunchFindsTests : IDisposable
{
    private static readonly AgentEntry _claude = Entry("claude-code", PolicyForm.ClaudeSettingsFile);
    private static readonly AgentEntry _cursor = Entry("cursor", PolicyForm.None);

    private readonly string _dir = Directory.CreateTempSubdirectory("aw-check-again-").FullName;
    private readonly List<ServiceProvider> _providers = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task The_fixture_self_check_passes_fs_deny_on_a_box_where_bwrap_runs()
    {
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        var home = Directory.CreateDirectory(Path.Combine(_dir, "homes", "owner")).FullName;   // why: the fixture lists the directories beside HOME as other homes, so HOME sits alone in a folder of its own
        string? Env(string name) => name switch
        {
            "HOME" => home,
            "AW_STATE_DIR" => Path.Combine(_dir, "state"),
            "XDG_RUNTIME_DIR" => Path.Combine(_dir, "run"),
            _ => Environment.GetEnvironmentVariable(name),
        };

        var services = Composition(Approved(target: null), [_claude])
            .AddSingleton(new SindriSettings(Path.Combine(_dir, "sindri"), Path.Combine(_dir, "sindri", "profile.yaml")))
            .AddSingleton<TimeProvider>(RealTime.Clock)
            .AddSindriPlatformSandbox(Env);
        await using var scope = Build(services).CreateAsyncScope();
        Assert.SkipUnless(scope.ServiceProvider.GetRequiredService<ISandbox>().Probe().IsSuccess, "bubblewrap's probe refuses on this box");

        var report = await scope.ServiceProvider.GetRequiredService<ISandboxChecks>().Run(Token);

        var said = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(report.Blockers.Select(SandboxBlockerLine.From).ToList(), SliceHarness.Json));
        Assert.DoesNotContain(report.Blockers, b => string.Equals(b.Reason, SandboxReasons.FsDeny, StringComparison.Ordinal));
        Assert.DoesNotContain("Can't find source path", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_form_check_skips_an_installed_agent_no_Step_runs()
    {
        var blockers = await FormCheck(Approved("claude-code"), _claude, _cursor).Check(Token);

        Assert.DoesNotContain(blockers, b => string.Equals(b.Reason, SandboxReasons.FormNone, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_form_check_names_the_target_it_blocks_on()
    {
        var blockers = await FormCheck(Approved("cursor"), _claude, _cursor).Check(Token);

        var blocker = Assert.Single(blockers, b => string.Equals(b.Code.Error.Code, "SND-LAUNCH-015", StringComparison.Ordinal) && string.Equals(b.Reason, SandboxReasons.FormNone, StringComparison.Ordinal));
        Assert.Contains("cursor", SandboxBlockerLine.From(blocker).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_canary_blocker_says_what_failed()
    {
        const string Observed = "visible: /home/owner/.ssh/id_ed25519";
        var sandbox = new FailingSelfCheck(SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsDeny, Observed));
        var checks = new SandboxChecks(sandbox, new NoBlockers(), new NoBlockers(), new FixedFixture(SandboxRequests.ReadOnly), new SandboxResumeList(new NoParks(), new FreeSlots()));

        var response = (await new CheckSandboxService(checks, new LastSandboxCheck()).Handle(new CheckSandboxRequest(), Token)).Value;

        Assert.Contains(Observed, System.Text.Encoding.UTF8.GetString(AwJson.Serialize(response, SliceHarness.Json)), StringComparison.Ordinal);
    }

    private ISandboxFormCheck FormCheck(IApprovedProfile approved, params AgentEntry[] installed)
    {
        var services = Composition(approved, installed).AddSingleton<TimeProvider>(RealTime.Clock).AddSingleton<ISandbox, PassThroughSandbox>();
        return Build(services).CreateScope().ServiceProvider.GetRequiredService<ISandboxFormCheck>();
    }

    /// <summary>The Check again registration serve makes, with fakes for what a test cannot build: the catalog, the approved profile, the login check and the parked Steps.</summary>
    private static IServiceCollection Composition(IApprovedProfile approved, params AgentEntry[] installed)
    {
        var services = new ServiceCollection()
            .AddCheckSandbox()
            .AddSingleton<IAgentCatalog>(new Catalog(installed))
            .AddSingleton<ILaunchSetup>(new Setup(installed))
            .AddSingleton(approved);
        return services
            .AddScoped<ISandboxLoginCheck, NoBlockers>().AddScoped<ISandboxParks, NoParks>().AddScoped<ISandboxSlots, FreeSlots>();
    }

    private ServiceProvider Build(IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider;
    }

    private static AgentEntry Entry(string id, PolicyForm policy) => new(AgentId.From(id), id, id, ProcessName: null, Transcripts: null, Resume: null) { PolicyForm = policy };

    private static ApprovedProfileFake Approved(string? target) => new(new LoadedProfile(
        new SindriProfile(1, "thor", Workflow: target is null ? null : new WorkflowSettings(Agent: new WorkflowAgentSettings(Target: AgentId.From(target)))),
        ProfileHash.From(new string('a', 64)),
        "/home/profile.yaml"));

    private sealed class Catalog(AgentEntry[] entries) : IAgentCatalog
    {
        public IReadOnlyList<AgentEntry> All => entries;

        public AgentEntry? Resolve(AgentId id) => entries.FirstOrDefault(e => e.Id == id);
    }

    private sealed class Setup(AgentEntry[] entries) : ILaunchSetup
    {
        public ValueTask<Result<LaunchInitReport>> Init(LaunchInitRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<LaunchShowReport>> Show(AgentId? only, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<LaunchShowReport>>(new LaunchShowReport(
                "none", "test", "/home/launcher.json",
                [.. entries.Select(static e => new LaunchShowEntry(e.Id, CatalogSource.Builtin, e.Bin, "/usr/bin/" + e.Bin, ArgvHash: null, StepOk: true, ParkLine: null))],
                []));
    }

    private sealed class ApprovedProfileFake(LoadedProfile? profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>(profile);
    }

    private sealed class NoBlockers : ISandboxFormCheck, ISandboxLoginCheck
    {
        public ValueTask<IReadOnlyList<SandboxBlocker>> Check(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<SandboxBlocker>>([]);
    }

    private sealed class NoParks : ISandboxParks
    {
        public ValueTask<IReadOnlyList<ParkedOnSandbox>> Parked(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<ParkedOnSandbox>>([]);
    }

    private sealed class FreeSlots : ISandboxSlots
    {
        public ValueTask<int> Free(CancellationToken cancellationToken) => ValueTask.FromResult(3);
    }

    private sealed class FixedFixture(SandboxRequest request) : ISandboxFixture
    {
        public SandboxRequest? Request() => request;

        public SandboxRequest? WriteScopeRequest() => null;
    }

    private sealed class FailingSelfCheck(Error failure) : ISandbox
    {
        private readonly PassThroughSandbox _inner = new();

        public Result<SandboxCapabilities> Probe() => _inner.Probe();

        public Result<SandboxPlan> Plan(SandboxRequest request) => _inner.Plan(request);

        public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct) => Task.FromResult<Result<Unit>>(failure);

        public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv) => _inner.Wrap(plan, argv);

        public void Discard(SandboxPlan plan) => _inner.Discard(plan);
    }
}
