using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class AgentCliResolutionTests
{
    private static readonly FakeExecutableLocator _all = new("claude", "codex", "cursor-agent");

    [Fact]
    public void Each_agent_cli_maps_to_its_host_binary_in_the_default_order() => Assert.Equal(["claude", "codex", "cursor-agent"], AgentCliNames.DefaultOrder.Select(AgentCliNames.Binary), StringComparer.Ordinal);

    [Fact]
    public void The_default_order_is_filtered_to_installed_binaries()
    {
        Assert.Equal([AgentCliName.ClaudeCli, AgentCliName.CodexCli, AgentCliName.CursorCli], AgentCliResolution.Resolve(_all, hostProvider: null, configured: null));
        Assert.Equal([AgentCliName.CodexCli, AgentCliName.CursorCli], AgentCliResolution.Resolve(new FakeExecutableLocator("cursor-agent", "codex"), hostProvider: null, configured: null));
    }

    [Fact]
    public void The_aw_provider_host_moves_to_the_front_but_is_still_filtered()
    {
        Assert.Equal([AgentCliName.CursorCli, AgentCliName.ClaudeCli, AgentCliName.CodexCli], AgentCliResolution.Resolve(_all, "cursor", configured: null));
        Assert.Equal([AgentCliName.ClaudeCli], AgentCliResolution.Resolve(new FakeExecutableLocator("claude"), "codex", configured: null));
        Assert.Equal([AgentCliName.ClaudeCli, AgentCliName.CodexCli, AgentCliName.CursorCli], AgentCliResolution.Resolve(_all, "nonsense", configured: null));
    }

    [Fact]
    public void An_explicit_config_list_wins_deduplicated_and_filtered()
    {
        var configured = new[] { AgentCliName.CodexCli, AgentCliName.CursorCli, AgentCliName.CodexCli, AgentCliName.ClaudeCli };

        Assert.Equal([AgentCliName.CodexCli, AgentCliName.ClaudeCli], AgentCliResolution.Resolve(new FakeExecutableLocator("codex", "claude"), "claude", configured));
        Assert.Empty(AgentCliResolution.Resolve(_all, hostProvider: null, []));
    }

    [Theory]
    [InlineData("codex", AgentCliName.CodexCli)]
    [InlineData(" Cursor ", AgentCliName.CursorCli)]
    [InlineData("claude-cli", AgentCliName.ClaudeCli)]
    [InlineData("CLAUDE-CLI", AgentCliName.ClaudeCli)]
    public void Aw_provider_accepts_host_and_provider_names_in_any_case(string value, AgentCliName expected) => Assert.Equal(expected, AgentCliNames.FromHost(value));

    [Theory]
    [InlineData(null)]
    [InlineData("gemini")]
    [InlineData("jev")]
    public void Aw_provider_ignores_unset_and_unknown_values(string? value) => Assert.Null(AgentCliNames.FromHost(value));
}
