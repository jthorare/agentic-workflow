using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Profile;
using Aw.Sindri.Tests.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>
/// The profile the first AddRepo creates (setup-once spec §5.1): it reads back as a valid profile, its 'workflow.effects' is the union of the effect kinds the shipped recipes declare, and a CI check fails when a
/// shipped recipe declares an effect the default doesn't allow.
/// </summary>
public sealed class DefaultProfileTests
{
    private static IReadOnlyList<RecipeDocument> Shipped { get; } =
        [.. ShippedRecipes.Recipes.Where(static p => p.StartsWith("sindri/", StringComparison.Ordinal)).Select(static p => RecipeComposition.ReadClean(ShippedRecipes.Read(p), Path.GetFileName(p)))];

    private static LoadedProfile Load(string yaml)
    {
        Assert.Null(YamlToJson.TryConvert(yaml, out var json));
        var loaded = ProfileReading.FromJson(json, "profile.yaml", ProfileFiles.Options);
        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? null : loaded.Error.Message);
        return loaded.Value;
    }

    private static IEnumerable<string> Undeclared(LoadedProfile profile) =>
        ShippedEffects.Union(Shipped.Select(static d => d.Recipe)).Where(e => EffectSettings.Of(profile.Profile).Allowlist?.Contains(BlockId.From(e)) != true);

    [Fact, Trait("Lane", "unit")]
    public void A_new_profile_reads_back_with_the_user_and_the_effects()
    {
        var loaded = Load(DefaultProfile.Text("thor", ["effect.notify.desk", "effect.claim.ref"]));

        Assert.Equal((1, "thor"), (loaded.Profile.SchemaVersion, loaded.Profile.User));
        Assert.Equal(["effect.claim.ref", "effect.notify.desk"], loaded.Profile.Workflow!.Effects);
        Assert.Empty(loaded.Profile.Repos!);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_user_name_with_a_quote_or_a_colon_stays_one_scalar()
        => Assert.Equal("a \"b\": c", Load(DefaultProfile.Text("a \"b\": c", [])).Profile.User);

    [Fact, Trait("Lane", "unit")]
    public void With_no_shipped_effects_the_key_is_an_empty_allowlist_not_absent()
    {
        var loaded = Load(DefaultProfile.Text("thor", []));

        Assert.Empty(loaded.Profile.Workflow!.Effects!);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_union_is_every_effect_a_shipped_recipe_declares_sorted_and_once()
    {
        var union = ShippedEffects.Union(Shipped.Select(static d => d.Recipe));

        Assert.Contains("effect.claim.ref", union, StringComparer.Ordinal);
        Assert.Equal(union.Order(StringComparer.Ordinal), union, StringComparer.Ordinal);
        Assert.Equal(union.Distinct(StringComparer.Ordinal).Count(), union.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_default_profile_allows_every_effect_the_shipped_recipes_declare()
    {
        var union = ShippedEffects.Union(Shipped.Select(static d => d.Recipe));

        Assert.Empty(Undeclared(Load(DefaultProfile.Text("thor", union))));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_check_fails_when_a_shipped_recipe_declares_an_effect_the_default_does_not_allow()
    {
        var union = ShippedEffects.Union(Shipped.Select(static d => d.Recipe));
        var stale = Load(DefaultProfile.Text("thor", ["effect.notify.desk"]));

        Assert.Equal(union.Where(static e => e != "effect.notify.desk"), Undeclared(stale), StringComparer.Ordinal);
        Assert.Contains("effect.claim.ref", Undeclared(stale), StringComparer.Ordinal);
    }

    /// <summary>
    /// ApproveShippedRecipe does not exist on this base (SO-E3), so this runs the check ApproveRecipe leans on: RCP-EFFECT-001, the profile's effect allowlist, over every shipped recipe under the default profile.
    /// The capability ceiling is not a rule of this base either.
    /// </summary>
    [Fact, Trait("Lane", "unit")]
    public void Every_shipped_recipe_passes_the_effect_allowlist_rule_under_the_default_profile()
    {
        using var provider = Composition.Compose(new ServiceCollection(), static name => name == "AW_STATE_DIR" ? Path.Combine(Path.GetTempPath(), "aw-sindri-recipes-test") : null, new FakeTimeProvider()).BuildServiceProvider();
        var catalog = provider.GetRequiredService<BlockCatalog>();
        var profile = Load(DefaultProfile.Text("thor", ShippedEffects.Union(Shipped.Select(static d => d.Recipe))));
        var facts = new RecipeProfileFacts(EffectSettings.Of(profile.Profile).Allowlist);

        var refused = Shipped.SelectMany(d => RecipeValidator.Validate(d, new ValidationContext(catalog, facts, new InMemoryChildRecipes(), BaseRecipe: null)))
            .Where(static f => f.Rule == "RCP-EFFECT-001" && f.Severity == FindingSeverity.Error)
            .Select(static f => f.ToLine());

        Assert.Empty(refused);
    }
}
