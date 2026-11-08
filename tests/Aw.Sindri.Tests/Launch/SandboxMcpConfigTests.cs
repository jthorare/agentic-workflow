using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>Issue 2151: where a launch's MCP config is and who moves it. The launcher gives a sandboxed claude the config's place in the session home; the sandbox launch moves the staged file there; an unsandboxed or MCP-less launch is unchanged.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxMcpConfigTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-sandbox-mcp-").FullName;
    private readonly FakeSessionHost _host = new();
    private string SindriHome => Path.Combine(_dir, "sindri-home");

    private string Launched => Path.Combine(_dir, "home", "launch");

    public void Dispose()
    {
        if (Directory.Exists(Launched))
        {
            File.SetUnixFileMode(Launched, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);   // a test made it read-only
        }

        Directory.Delete(_dir, recursive: true);
    }

    private Launcher Make(ISandboxLaunch? sandbox, McpForm form) =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode with { McpForm = form }]), _ => _host, new FakeTerminalViewer(), new LauncherOptions(Path.Combine(_dir, "home")) { SindriHome = SindriHome }, new FakeTimeProvider(KnownInstants.Epoch))
        {
            Sandbox = sandbox,
            ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
            McpFor = static (_, _) => ValueTask.FromResult<McpTarget?>(new McpTarget("web", 7410)),
        });

    private async Task<(LaunchRequest Request, IReadOnlyList<string> Argv)> Launch(ISandboxLaunch? sandbox, McpForm form)
    {
        var request = A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("scoping", "agent"))).InDirectory(_dir).Build();
        _ = await Make(sandbox, form).Launch(request, TestContext.Current.CancellationToken);
        return (request, Assert.Single(_host.Started).Argv);
    }

    private static string After(IReadOnlyList<string> argv, string flag) => argv[argv.ToList().IndexOf(flag) + 1];

    [Fact]
    public async Task A_sandboxed_claude_launch_is_given_the_config_s_place_in_its_session_home_and_stages_the_file_under_launch()
    {
        var sandbox = new FakeSandboxLaunch();
        var (request, argv) = await Launch(sandbox, McpForm.ClaudeConfigFile);

        var inside = Path.Combine(SessionHomePaths.For(SindriHome, request.Launch), $"mcp-{request.Launch.Value}.json");
        Assert.Equal(inside, After(argv, "--mcp-config"));
        Assert.Equal(inside, After(Assert.Single(sandbox.Prepared).Argv, "--mcp-config"));   // what the sandbox is handed, before it wraps
        Assert.True(File.Exists(Path.Combine(Launched, $"mcp-{request.Launch.Value}.json")), "the sandbox launch moves this file once the session home exists");
    }

    [Fact]
    public async Task A_launch_without_an_mcp_form_has_no_config_argument_and_no_file()
    {
        var (_, argv) = await Launch(new FakeSandboxLaunch(), McpForm.None);

        Assert.DoesNotContain(argv, a => string.Equals(a, "--mcp-config", StringComparison.Ordinal));
        Assert.Empty(Directory.Exists(Launched) ? Directory.EnumerateFiles(Launched, "mcp-*", SearchOption.AllDirectories) : []);
    }

    [Fact]
    public async Task An_unsandboxed_claude_launch_still_points_at_the_file_under_launch()
    {
        var (request, argv) = await Launch(sandbox: null, McpForm.ClaudeConfigFile);

        var file = Path.Combine(Launched, $"mcp-{request.Launch.Value}.json");
        Assert.Equal(file, After(argv, "--mcp-config"));
        Assert.True(File.Exists(file));
    }

    private (SandboxLaunch Launch, SandboxRequest Request, string Staged) MakeSandboxLaunch()
    {
        var runArea = Directory.CreateDirectory(Path.Combine(SindriHome, "workflow", "areas", "run-1")).FullName;   // the plan refuses a run area outside the Sindri home
        var runDirectory = Directory.CreateDirectory(Path.Combine(SindriHome, "workflow", "runs", "1")).FullName;
        var launchDirectory = Directory.CreateDirectory(Launched).FullName;
        var secrets = Directory.CreateDirectory(Path.Combine(launchDirectory, "secrets")).FullName;
        var request = SandboxRequests.WriteScope with
        {
            Host = SandboxRequests.WriteScope.Host with { SindriHome = SindriHome },
            Paths = SandboxRequests.WriteScope.Paths with { RunDirectory = runDirectory, RunArea = runArea },
        };
        File.WriteAllText(LaunchSecrets.SecretPathFor(secrets, request.Launch), "secret");
        var control = new ControlEndpoint(new Uri("http://127.0.0.1:7480"), Environment.ProcessId, Epoch: 0);
        var launch = new SandboxLaunch(
            new PassThroughSandbox(), new FileSessionCap(Path.Combine(_dir, "sessions.lock"), new FakeSliceScopes()), new FixedStages(request),
            new SandboxLaunchOptions("systemd-run", new BoxFacts(32L * 1024 * 1024 * 1024, 8), SindriHome, secrets, () => control), new LaunchSecretRegistry());
        return (launch, request, McpLaunchConfig.PathFor(launchDirectory, request.Launch));
    }

    private static LaunchRequest Step() => A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent"))).Build();

    [Fact]
    public async Task The_sandbox_launch_moves_the_staged_config_into_the_session_home()
    {
        var (launch, request, staged) = MakeSandboxLaunch();
        await File.WriteAllTextAsync(staged, "{\"mcpServers\":{}}", TestContext.Current.CancellationToken);

        var prepared = await launch.Prepare(Step(), ["claude"], chosenSession: null, TestContext.Current.CancellationToken);

        Assert.True(prepared.IsSuccess, prepared.IsError ? prepared.Error.Message : null);
        var moved = McpLaunchConfig.PathFor(SessionHomePaths.For(SindriHome, request.Launch), request.Launch);
        Assert.Equal("{\"mcpServers\":{}}", await File.ReadAllTextAsync(moved, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(staged), "the staged copy under launch/ is gone");
    }

    [Fact]
    public async Task A_launch_with_no_staged_config_puts_no_mcp_file_in_the_session_home()
    {
        var (launch, request, _) = MakeSandboxLaunch();

        var prepared = await launch.Prepare(Step(), ["claude"], chosenSession: null, TestContext.Current.CancellationToken);

        Assert.True(prepared.IsSuccess, prepared.IsError ? prepared.Error.Message : null);
        Assert.Empty(Directory.EnumerateFiles(SessionHomePaths.For(SindriHome, request.Launch), "mcp-*"));
    }

    [Fact]
    public async Task A_config_that_cannot_be_moved_refuses_the_launch_as_fs_write()
    {
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "root can rename out of a read-only directory");
        var (launch, _, staged) = MakeSandboxLaunch();
        await File.WriteAllTextAsync(staged, "{}", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(Launched, UnixFileMode.UserRead | UnixFileMode.UserExecute);   // the rename out of launch/ is refused

        var prepared = await launch.Prepare(Step(), ["claude"], chosenSession: null, TestContext.Current.CancellationToken);

        Assert.True(prepared.IsError);
        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, prepared.Error.Code);
        Assert.Equal(SandboxReasons.FsWrite, SandboxRefusal.ReasonOf(prepared.Error));
    }

    private sealed class FixedStages(SandboxRequest request) : ISandboxLaunchStages
    {
        public ValueTask<Result<SandboxRequest>> Request(LaunchRequest launch, SessionId? chosenSession, CancellationToken cancellationToken) => ValueTask.FromResult<Result<SandboxRequest>>(request);

        public ValueTask<Result<PolicyFiles>> WritePolicy(SandboxRequest request, SandboxPlan plan, string sessionHome, CancellationToken cancellationToken) => ValueTask.FromResult<Result<PolicyFiles>>(PolicyFiles.None);

        public ValueTask<Result<Unit>> StartEgress(SandboxPlan plan, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);

        public ValueTask Release(LaunchId launch, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
