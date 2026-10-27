using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>Issue 1005, through Launcher.Prepare: an agent Step's launch writes its instance and run into its MCP file; a launch missing the Step, the instance or the run writes the repo header alone.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherMcpHeadersTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-mcp-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _host = new();
    private readonly FakeTerminalViewer _viewer = new();

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private static StepArgs Step() => new(Model: null, Policy: null, new StepRef("scoping", "agent"));

    private Launcher Make() =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode with { McpForm = McpForm.ClaudeConfigFile, Open = ["{bin}", "--session-id", "{sessionId}", "-n", "{name}", "--mcp-config", "{mcpConfig}", "{seed}"] }]), _ => _host, _viewer, new LauncherOptions(_home), _clock)
        {
            ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
            McpFor = static (_, _) => ValueTask.FromResult<McpTarget?>(new McpTarget("web", 7410)),
        });

    private async Task<string> McpFileOf(LaunchRequestBuilder request)
    {
        _ = await Make().Launch(request.InDirectory(_home).Build(), CancellationToken.None);
        var argv = _host.Started.Single().Argv;
        return await File.ReadAllTextAsync(argv[argv.ToList().IndexOf("--mcp-config") + 1], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_Step_launch_with_an_instance_and_run_writes_both_headers_into_its_mcp_file()
    {
        var text = await McpFileOf(A.LaunchRequest().Unattended(Step()).OnInstance(InstanceAddress.From("T-1/scoping/1"), RunToken.From("5.1")));

        Assert.Contains("\"X-Sindri-Repo\": \"web\"", text, StringComparison.Ordinal);
        Assert.Contains("\"X-Sindri-Instance\": \"T-1/scoping/1\"", text, StringComparison.Ordinal);
        Assert.Contains("\"X-Sindri-Run\": \"5.1\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_launch_with_an_instance_and_run_but_no_Step_writes_the_repo_header_alone()
    {
        var text = await McpFileOf(A.LaunchRequest().OnInstance(InstanceAddress.From("T-1/scoping/1"), RunToken.From("5.1")));

        Assert.Contains("X-Sindri-Repo", text, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Sindri-Instance", text, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Sindri-Run", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Step_launch_with_no_instance_or_run_writes_the_repo_header_alone()
    {
        var text = await McpFileOf(A.LaunchRequest().Unattended(Step()));

        Assert.Contains("X-Sindri-Repo", text, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Sindri-Instance", text, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Sindri-Run", text, StringComparison.Ordinal);
    }
}
