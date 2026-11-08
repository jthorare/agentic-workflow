using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>Issue 2151: a host path on the agent's command line (--mcp-config, --settings) must be visible inside the sandbox, not under the Sindri-home mask.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxPlanArgvPathTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-argv-path-").FullName;
    private readonly FakeSandboxLaunch _sandbox = new();

    public void Dispose() => Directory.Delete(_home, recursive: true);

    // The plan for the launch the launcher prepared: its id, and run folders inside the Sindri home as the planner requires.
    private static SandboxRequest RequestAt(string sindriHome, LaunchId launch)
    {
        var paths = Req.WriteScope.Paths with { RunDirectory = sindriHome + "/workflow/runs/x/s/1", RunArea = sindriHome + "/workflow/areas/run-1" };
        return Req.WriteScope with { Launch = launch, Host = Req.Host with { SindriHome = sindriHome }, Paths = paths };
    }

    // The last mount that covers the path decides: a bind shows it, a tmpfs or a deny over it hides it, and no mount at all leaves it absent.
    private static bool Visible(SandboxPlan plan, string path)
    {
        var covering = plan.Mounts.LastOrDefault(m => string.Equals(m.Path, path, StringComparison.Ordinal) || path.StartsWith(m.Path + "/", StringComparison.Ordinal));
        return covering is { Kind: PlanMountKind.ReadOnly or PlanMountKind.ReadWrite or PlanMountKind.PerLaunchDir };
    }

    [Fact]
    public async Task The_mcp_config_path_on_a_claude_launch_s_argv_is_visible_in_its_sandbox_plan()
    {
        var launcher = new Launcher(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode with { McpForm = McpForm.ClaudeConfigFile, Open = ["{bin}", "--session-id", "{sessionId}", "-n", "{name}", "--mcp-config", "{mcpConfig}", "{seed}"] }]), _ => new FakeSessionHost(), new FakeTerminalViewer(), new LauncherOptions(_home) { SindriHome = _home }, new FakeTimeProvider(KnownInstants.Epoch))
        {
            Sandbox = _sandbox,
            ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
            McpFor = static (_, _) => ValueTask.FromResult<McpTarget?>(new McpTarget("web", 7410)),
        });

        _ = await launcher.Launch(A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("scoping", "agent"))).InDirectory(_home).Build(), TestContext.Current.CancellationToken);

        var argv = Assert.Single(_sandbox.Prepared).Argv;
        var mcpConfig = argv[argv.ToList().IndexOf("--mcp-config") + 1];
        var launched = Assert.Single(_sandbox.Prepared).Request.Launch;
        var plan = SandboxPlanner.Build(SandboxMechanism.PassThrough, RequestAt(_home, launched));
        Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.Message : string.Empty);
        Assert.True(Visible(plan.Value, mcpConfig), $"--mcp-config {mcpConfig} is hidden by the Sindri-home mask");
    }

    [Fact]
    public void An_absolute_path_argument_under_a_masked_directory_is_refused_before_start()
    {
        var plan = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope).Value;
        var hidden = Req.SindriHome + "/launch/policy/settings.json";

        var refusal = PlanArgvPaths.Check(plan, ["claude", "--settings", hidden]);

        Assert.NotNull(refusal);
        Assert.Equal(SindriErrors.LaunchUnconfinable.Code, refusal.Code);
        Assert.Contains(hidden, refusal.Message, StringComparison.Ordinal);
    }
}
