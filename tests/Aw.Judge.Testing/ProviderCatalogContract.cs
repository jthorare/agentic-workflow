using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>The IProviderCatalog contract: <see cref="FakeProviderCatalog"/> and the real catalog both pass it.</summary>
public abstract class ProviderCatalogContract
{
    /// <summary>A catalog with Jev on which exactly these agent CLIs are installed (AW_PROVIDER unset).</summary>
    /// <param name="installed">The installed agent CLIs, in the default order.</param>
    /// <returns>The catalog.</returns>
    protected abstract IProviderCatalog Create(IReadOnlyList<AgentCliName> installed);

    /// <summary>The rules provider every Lineup call passes (the engine's own, played by a ScriptedProvider).</summary>
    protected static IModelProvider Rules { get; } =
        new ScriptedProvider(ProviderName.Rules, [.. ContentClasses.All]).Returns(new ProviderUnavailable("no-rule"));

    /// <summary>Every class ends with rules, and every name in the chain has its provider in the lineup.</summary>
    [Fact]
    public void Rules_ends_every_class_and_every_chain_name_has_a_provider()
    {
        var lineup = Create([AgentCliName.ClaudeCli]).Lineup(config: null, allowed: null, Rules);

        foreach (var contentClass in ContentClasses.All)
        {
            var names = lineup.Chain.For(contentClass);
            Expect.That(names.Count > 0 && names[^1] == ProviderName.Rules, $"{ContentClasses.Name(contentClass)} ends with rules");
            Expect.That(names.All(n => lineup.Providers.Where(p => p.Name == n).Take(2).Count() == 1), $"every provider of {ContentClasses.Name(contentClass)} is in the lineup once");
        }
    }

    /// <summary>Text goes to Jev, then the installed agent CLIs, then rules; image skips Jev.</summary>
    [Fact]
    public void Only_installed_agent_clis_are_chained_after_jev()
    {
        var chain = Create([AgentCliName.CodexCli]).Lineup(config: null, allowed: null, Rules).Chain;

        Expect.That(chain.For(ContentClass.MessageMeta).SequenceEqual([ProviderName.Jev, ProviderName.CodexCli, ProviderName.Rules]), "text: jev, codex, rules");
        Expect.That(chain.For(ContentClass.Image).SequenceEqual([ProviderName.CodexCli, ProviderName.Rules]), "image: codex, rules");
    }

    /// <summary>The allowlist keeps the allowed providers and rules, in the lineup and the chain.</summary>
    [Fact]
    public void The_allowlist_keeps_only_the_allowed_providers_and_rules()
    {
        var lineup = Create([AgentCliName.ClaudeCli, AgentCliName.CodexCli]).Lineup(config: null, allowed: [ProviderName.CodexCli], Rules);

        Expect.That(lineup.Chain.For(ContentClass.Code).SequenceEqual([ProviderName.CodexCli, ProviderName.Rules]), "code: codex, rules");
        Expect.That(lineup.Providers.Select(p => p.Name).Order().SequenceEqual([ProviderName.Rules, ProviderName.CodexCli]), "only codex and rules are in the lineup");
    }

    /// <summary>config.json can turn Jev off and pick the agent CLIs.</summary>
    [Fact]
    public void Config_turns_jev_off_and_picks_the_agent_clis()
    {
        var config = new ProvidersConfig(AgentClis: ["codex-cli"], Jev: false);

        var lineup = Create([AgentCliName.ClaudeCli, AgentCliName.CodexCli]).Lineup(config, allowed: null, Rules);

        Expect.That(lineup.Chain.For(ContentClass.Diff).SequenceEqual([ProviderName.CodexCli, ProviderName.Rules]), "diff: codex, rules");
        Expect.That(lineup.Providers.All(p => p.Name is not ProviderName.Jev and not ProviderName.ClaudeCli), "jev and claude are left out");
    }
}
