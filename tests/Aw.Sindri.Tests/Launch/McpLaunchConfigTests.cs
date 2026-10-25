using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>What an agent is told about Sindri's MCP endpoint at launch (launcher spec section 3, the 'mcp' form).</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class McpLaunchConfigTests : IDisposable
{
    private static readonly McpTarget _target = new("web", 7410);
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));

    private readonly string _directory = Directory.CreateTempSubdirectory("aw-mcp-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Mcp_config_has_no_token_and_is_swept()
    {
        var path = McpLaunchConfig.Write(_directory, _launch, _target);
        var text = File.ReadAllText(path);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        Assert.Contains("http://127.0.0.1:7410/sindri/mcp", text, StringComparison.Ordinal);
        Assert.Contains("\"X-Sindri-Repo\": \"web\"", text, StringComparison.Ordinal);
        Assert.Contains("\"X-Sindri-Launch\": \"" + _launch.Value + "\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-Sindri-Run", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri-step", text, StringComparison.Ordinal);

        Assert.Equal(0, McpLaunchConfig.Sweep(_directory, RealTime.NowOffset, TimeSpan.FromHours(1), static _ => false));   // too young
        Assert.Equal(0, McpLaunchConfig.Sweep(_directory, RealTime.NowOffset.AddDays(1), TimeSpan.FromHours(1), static _ => true));   // live
        Assert.Equal(1, McpLaunchConfig.Sweep(_directory, RealTime.NowOffset.AddDays(1), TimeSpan.FromHours(1), static _ => false));
        Assert.False(File.Exists(path));
        McpLaunchConfig.Delete(_directory, _launch);   // a missing file is fine
    }

    [Fact]
    public void Each_form_adds_its_arguments_and_none_adds_nothing()
    {
        var claude = McpLaunchConfig.Arguments(McpForm.ClaudeConfigFile, _target, _launch, "/home/x/launch/mcp-LCH.json");
        var codex = McpLaunchConfig.Arguments(McpForm.CodexOverrides, _target, _launch, configPath: null);

        Assert.Equal(["--mcp-config", "/home/x/launch/mcp-LCH.json"], claude);
        Assert.Equal(["-c", "mcp_servers.sindri.url=\"http://127.0.0.1:7410/sindri/mcp\"", "-c", "mcp_servers.sindri.http_headers={ X-Sindri-Repo = \"web\", X-Sindri-Launch = \"" + _launch.Value + "\" }"], codex);
        Assert.Empty(McpLaunchConfig.Arguments(McpForm.None, _target, _launch, configPath: null));
        Assert.DoesNotContain(codex, static a => a.Contains("X-Sindri-Run", StringComparison.Ordinal));
    }
}
