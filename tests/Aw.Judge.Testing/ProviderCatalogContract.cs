using Aw.Judge.Core.Config;
using Aw.Judge.Core.Errors;
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

    /// <summary>A provider asked for by name is the installed provider of that name, with no chain around it.</summary>
    [Fact]
    public void A_provider_asked_for_by_name_is_the_installed_provider_of_that_name()
    {
        var catalog = Create([AgentCliName.ClaudeCli, AgentCliName.CodexCli]);

        var codex = catalog.Provider(ProviderName.CodexCli, allowed: null);
        var jev = catalog.Provider(ProviderName.Jev, allowed: [ProviderName.Jev]);

        Expect.That(codex.IsSuccess && codex.Value.Name == ProviderName.CodexCli, "codex-cli is the codex provider");
        Expect.That(jev.IsSuccess && jev.Value.Name == ProviderName.Jev, "jev is the jev provider");
    }

    /// <summary>The allowlist is honoured: an excluded provider is AW-JUDGE-012, even when it is installed.</summary>
    [Fact]
    public void A_provider_asked_for_by_name_honours_the_allowlist()
    {
        var excluded = Create([AgentCliName.ClaudeCli]).Provider(ProviderName.ClaudeCli, allowed: [ProviderName.Jev]);

        Expect.That(excluded.IsError && excluded.Error.Code == JudgeErrors.ProviderExcluded.Code, "claude-cli is excluded by an allowlist of jev");
    }

    /// <summary>An agent CLI that isn't installed, and rules, can't be asked by name: AW-JUDGE-013.</summary>
    [Fact]
    public void A_provider_that_is_not_installed_or_is_rules_is_unavailable_by_name()
    {
        var catalog = Create([AgentCliName.ClaudeCli]);

        var codex = catalog.Provider(ProviderName.CodexCli, allowed: null);
        var rules = catalog.Provider(ProviderName.Rules, allowed: null);

        Expect.That(codex.IsError && codex.Error.Code == JudgeErrors.ProviderUnavailable.Code, "codex-cli is not installed");
        Expect.That(rules.IsError && rules.Error.Code == JudgeErrors.ProviderUnavailable.Code, "rules only closes a chain");
    }

    /// <summary>The adjudicator is claude-cli, with the same refusals by name as any provider: AW-JUDGE-012 and AW-JUDGE-013.</summary>
    [Fact]
    public void The_adjudicator_is_claude_and_honours_the_allowlist_and_the_path()
    {
        var found = Create([AgentCliName.ClaudeCli]).Adjudicator(allowed: null);
        var excluded = Create([AgentCliName.ClaudeCli]).Adjudicator(allowed: [ProviderName.Jev]);
        var missing = Create([AgentCliName.CodexCli]).Adjudicator(allowed: null);

        Expect.That(found.IsSuccess && found.Value.Name == ProviderName.ClaudeCli, "the adjudicator is claude-cli");
        Expect.That(excluded.IsError && excluded.Error.Code == JudgeErrors.ProviderExcluded.Code, "an allowlist without claude-cli excludes it");
        Expect.That(missing.IsError && missing.Error.Code == JudgeErrors.ProviderUnavailable.Code, "claude-cli not installed is unavailable");
    }
}
