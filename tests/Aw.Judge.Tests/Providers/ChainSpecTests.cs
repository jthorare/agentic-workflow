using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class ChainSpecTests
{
    private static readonly ProviderName[] _textDefault = [ProviderName.Jev, ProviderName.ClaudeCli, ProviderName.Rules];

    [Fact]
    public void The_default_chain_is_jev_claude_rules_on_text_and_claude_rules_on_image()
    {
        foreach (var cls in ContentClasses.Text)
        {
            Assert.Equal(_textDefault, ChainSpec.Default.For(cls));
        }

        Assert.Equal([ProviderName.ClaudeCli, ProviderName.Rules], ChainSpec.Default.For(ContentClass.Image));
    }

    [Fact]
    public void Build_puts_jev_first_then_the_agent_clis_in_order_then_rules_and_image_skips_jev()
    {
        var chain = ChainSpec.Build([AgentCliName.CodexCli, AgentCliName.ClaudeCli, AgentCliName.CursorCli], jev: true);

        foreach (var cls in ContentClasses.Text)
        {
            Assert.Equal([ProviderName.Jev, ProviderName.CodexCli, ProviderName.ClaudeCli, ProviderName.CursorCli, ProviderName.Rules], chain.For(cls));
        }

        Assert.Equal([ProviderName.CodexCli, ProviderName.ClaudeCli, ProviderName.CursorCli, ProviderName.Rules], chain.For(ContentClass.Image));
    }

    [Fact]
    public void Build_drops_jev_when_disabled_and_degrades_to_rules_with_no_agent_clis()
    {
        Assert.Equal([ProviderName.CursorCli, ProviderName.Rules], ChainSpec.Build([AgentCliName.CursorCli], jev: false).For(ContentClass.Code));

        var bare = ChainSpec.Build([], jev: false);
        Assert.Equal([ProviderName.Rules], bare.For(ContentClass.MessageMeta));
        Assert.Equal([ProviderName.Rules], bare.For(ContentClass.Image));
    }

    [Fact]
    public void Restrict_keeps_only_allowed_providers_and_always_rules()
    {
        var chain = ChainSpec.Build([AgentCliName.ClaudeCli, AgentCliName.CodexCli], jev: true);

        var restricted = chain.Restrict([ProviderName.Jev, ProviderName.ClaudeCli]);
        Assert.Equal(_textDefault, restricted.For(ContentClass.Code));
        Assert.Equal([ProviderName.ClaudeCli, ProviderName.Rules], restricted.For(ContentClass.Image));

        Assert.Equal([ProviderName.Rules], chain.Restrict([]).For(ContentClass.Code));
    }

    [Fact]
    public void ProvidersFor_keeps_chain_order_and_only_providers_present_that_cover_the_class()
    {
        var imageOnly = new ScriptedProvider(ProviderName.Jev, ContentClass.Image);
        var rules = new ScriptedProvider(ProviderName.Rules, ContentClass.Code);
        var cli = new ScriptedProvider(ProviderName.ClaudeCli, ContentClass.Code);

        var candidates = ChainRouting.ProvidersFor(ChainSpec.Default, ContentClass.Code, [rules, imageOnly, cli]);

        Assert.Equal([ProviderName.ClaudeCli, ProviderName.Rules], candidates.Select(p => p.Name));
    }

    [Fact]
    public void ProvidersFor_is_empty_for_a_class_the_chain_does_not_route()
    {
        var chain = new ChainSpec(new Dictionary<ContentClass, IReadOnlyList<ProviderName>>());

        Assert.Empty(ChainRouting.ProvidersFor(chain, ContentClass.Code, [new ScriptedProvider(ProviderName.Rules, ContentClass.Code)]));
    }

    [Fact]
    public void Wire_names_round_trip()
    {
        foreach (var name in Enum.GetValues<ProviderName>())
        {
            Assert.True(ProviderNames.TryParse(ProviderNames.Name(name), out var parsed));
            Assert.Equal(name, parsed);
        }

        Assert.Equal("message-meta", ContentClasses.Name(ContentClass.MessageMeta));
        Assert.False(ProviderNames.TryParse("Jev", out _));
    }
}
