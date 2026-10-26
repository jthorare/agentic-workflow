using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>Issue 1005: an agent Step's session reaches /sindri/mcp with its instance and run as well as its repo, so the context tools can scope to its graph. A plain session carries the repo alone.</summary>
[Lane(TestLane.Unit)]
public sealed class McpLaunchHeadersTests
{
    private static readonly McpTarget _step = new("web", 7410, "T-1/scoping/1", "5.1");
    private static readonly McpTarget _plain = new("web", 7410);
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));

    [Fact]
    public void A_Steps_Claude_file_carries_the_repo_instance_and_run_headers()
    {
        var text = Encoding.UTF8.GetString(McpLaunchConfig.Render(_step, _launch));

        Assert.Contains("\"X-Sindri-Repo\": \"web\"", text, StringComparison.Ordinal);
        Assert.Contains("\"X-Sindri-Instance\": \"T-1/scoping/1\"", text, StringComparison.Ordinal);
        Assert.Contains("\"X-Sindri-Run\": \"5.1\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Steps_Codex_arguments_carry_both_new_headers()
    {
        var codex = McpLaunchConfig.Arguments(McpForm.CodexOverrides, _step, _launch, configPath: null);

        Assert.Contains(codex, static a => a.Contains("X-Sindri-Repo = \"web\"", StringComparison.Ordinal) && a.Contains("X-Sindri-Instance = \"T-1/scoping/1\"", StringComparison.Ordinal) && a.Contains("X-Sindri-Run = \"5.1\"", StringComparison.Ordinal));
    }

    [Fact]
    public void A_plain_session_carries_only_the_repo_header_in_both_forms()
    {
        var claude = Encoding.UTF8.GetString(McpLaunchConfig.Render(_plain, _launch));
        var codex = string.Concat(McpLaunchConfig.Arguments(McpForm.CodexOverrides, _plain, _launch, configPath: null));

        Assert.DoesNotContain("X-Sindri-Instance", claude + codex, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Sindri-Run", claude + codex, StringComparison.Ordinal);
        Assert.Contains("X-Sindri-Repo", claude + codex, StringComparison.Ordinal);
    }
}
