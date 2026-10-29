using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Features;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.CheckSandbox;

/// <summary>
/// Issue #1579: Check again's fixture self-check request carries no login, so the plan never binds the agent home and the hooks-run canary cannot find ~/.claude/hooks. The request has to carry a login
/// prepared the way a real launch's is, and Check again has to release it once its self-checks are done.
/// </summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class CheckAgainPreparesTheFixtureLoginTests : IDisposable
{
    private static readonly AgentEntry _claude = Entry("claude-code", PolicyForm.ClaudeSettingsFile);

    private readonly string _dir = Directory.CreateTempSubdirectory("aw-check-again-login-").FullName;
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
    public async Task Check_again_plans_its_fixtures_with_the_login_a_launch_prepares()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the Linux fixture is registered on Linux only");
        var (recorder, home) = await RunCheck();

        Assert.NotEmpty(recorder.Requests);
        Assert.All(recorder.Requests, request =>
        {
            Assert.NotNull(request.Login);
            Assert.Equal(ModelLoginSource.OwnerCopy, request.Login.Source);
            Assert.Equal(AgentId.From("claude-code"), request.Target);
            Assert.Contains(request.Login.Mounts, m => string.Equals(m.SandboxPath, Path.Combine(home, ".claude"), StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task Check_again_releases_the_fixture_login_after_its_self_checks()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the Linux fixture is registered on Linux only");
        var (recorder, _) = await RunCheck();

        Assert.Contains(recorder.Requests, r => r.Login is not null);   // why: without it the next assertion passes vacuously
        var launches = Path.Combine(_dir, "sindri", "launches");
        var left = Directory.Exists(launches) ? Directory.EnumerateDirectories(launches).Where(d => Directory.Exists(Path.Combine(d, "login"))).ToList() : [];
        Assert.Empty(left);
    }

    private async Task<(RecordingSandbox Recorder, string Home)> RunCheck()
    {
        var home = Directory.CreateDirectory(Path.Combine(_dir, "homes", "owner")).FullName;   // why: the fixture lists the directories beside HOME as other homes, so HOME sits alone in a folder of its own
        Directory.CreateDirectory(Path.Combine(home, ".claude", "hooks"));
        await File.WriteAllTextAsync(Path.Combine(home, ".claude", ".credentials.json"), "{\"claudeAiOauth\":{\"expiresAt\":4102444800000,\"accessToken\":\"x\",\"refreshToken\":\"y\"}}", Token);
        string? Env(string name) => name switch
        {
            "HOME" => home,
            "AW_STATE_DIR" => Path.Combine(_dir, "state"),
            "XDG_RUNTIME_DIR" => Path.Combine(_dir, "run"),
            _ => Environment.GetEnvironmentVariable(name),
        };

        var recorder = new RecordingSandbox();
        var services = Composition(Approved("claude-code"), [_claude])
            .AddSingleton(new SindriSettings(Path.Combine(_dir, "sindri"), Path.Combine(_dir, "sindri", "profile.yaml")))
            .AddSingleton(RealTime.Clock)
            .AddSindriPlatformSandbox(Env)
            .AddSingleton<ModelCredentialSources>();
        _ = services.RemoveAll<ISandbox>().AddSingleton<ISandbox>(recorder);
        await using var scope = Build(services).CreateAsyncScope();

        _ = await scope.ServiceProvider.GetRequiredService<ISandboxChecks>().Run(Token);

        return (recorder, home);
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
}
