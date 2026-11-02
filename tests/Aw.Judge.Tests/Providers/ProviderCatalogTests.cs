using Aw.Judge.Core.Config;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class ProviderCatalogTests
{
    private static readonly ScriptedProvider _rules = new(ProviderName.Rules, [.. ContentClasses.All]);

    private static readonly AgentCliName[] _all = [AgentCliName.ClaudeCli, AgentCliName.CodexCli, AgentCliName.CursorCli];

    [Fact]
    public void The_aw_provider_host_leads_the_agent_clis()
    {
        var chain = Catalog(_all, hostProvider: "codex").Lineup(config: null, allowed: null, _rules).Chain;

        Assert.Equal([ProviderName.Jev, ProviderName.CodexCli, ProviderName.ClaudeCli, ProviderName.CursorCli, ProviderName.Rules], chain.For(ContentClass.Transcript));
    }

    [Fact]
    public void A_config_list_wins_over_the_host()
    {
        var chain = Catalog(_all, hostProvider: "codex").Lineup(new ProvidersConfig(AgentClis: ["cursor-cli", "claude-cli"]), allowed: null, _rules).Chain;

        Assert.Equal([ProviderName.CursorCli, ProviderName.ClaudeCli, ProviderName.Rules], chain.For(ContentClass.Image));
    }

    [Fact]
    public void The_lineup_holds_the_real_adapters_and_the_callers_rules()
    {
        var lineup = Catalog(_all, hostProvider: null).Lineup(config: null, allowed: null, _rules);

        Assert.Equal(
            [typeof(JevProvider), typeof(ClaudeCliProvider), typeof(CodexCliProvider), typeof(CursorCliProvider)],
            lineup.Providers.SkipLast(1).Select(p => Assert.IsType<FailOpenModelProvider>(p).Inner.GetType()));
        Assert.Same(_rules, lineup.Providers[^1]);
    }

    [Fact]
    public void A_provider_that_is_not_rules_is_refused_as_rules() =>
        Assert.Throws<ArgumentException>(() => Catalog(_all, hostProvider: null).Lineup(config: null, allowed: null, new ScriptedProvider(ProviderName.Jev)));

    [Fact]
    public void The_adjudicator_is_the_claude_provider_on_opus_and_high_effort()
    {
        var provider = Catalog(_all, hostProvider: null).Adjudicator(allowed: null);

        Assert.Equal(ClaudeCliOptions.Adjudicator, Assert.IsType<ClaudeCliProvider>(Assert.IsType<FailOpenModelProvider>(provider.Value).Inner).Options);
    }

    [Fact]
    public void The_adjudicator_needs_claude_on_the_path_and_in_the_allowlist()
    {
        Assert.Equal(JudgeErrors.ProviderUnavailable.Code, Catalog([AgentCliName.CodexCli], hostProvider: null).Adjudicator(allowed: null).Error.Code);
        Assert.Equal(JudgeErrors.ProviderExcluded.Code, Catalog(_all, hostProvider: null).Adjudicator(allowed: [ProviderName.Jev]).Error.Code);
    }

    internal static ProviderCatalog Catalog(IReadOnlyList<AgentCliName> installed, string? hostProvider)
    {
        var runner = new FakeProcessRunner();
        return new ProviderCatalog(
            name => name == "AW_PROVIDER" ? hostProvider : null,
            new FakeExecutableLocator([.. installed.Select(AgentCliNames.Binary)]),
            [
                new JevProvider(new HttpClient(), new FakeSecretSource(), new FakeInputRedactor()),
                new ClaudeCliProvider(runner),
                new CodexCliProvider(runner),
                new CursorCliProvider(runner, new FakeInputRedactor()),
            ],
            new ClaudeCliProvider(runner) { Options = ClaudeCliOptions.Adjudicator });
    }
}
