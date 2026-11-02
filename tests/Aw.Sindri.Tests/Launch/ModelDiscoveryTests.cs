using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.Launch;

public sealed class ModelDiscoveryTests
{
    private static string CodexFixture => File.ReadAllText(Path.Combine(SessionFixtures.Launcher, "models", "codex-models.json"));

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_keeps_the_listed_models_in_printed_order()
    {
        var models = CodexModels.Read(CodexFixture);

        Assert.Equal(["gpt-5.5", "gpt-5.4", "gpt-5.4-mini", "gpt-5.3-codex", "gpt-5.2"], models.Select(m => m.Slug), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_drops_hidden_models() =>
        Assert.DoesNotContain(CodexModels.Read(CodexFixture), m => string.Equals(m.Slug, "codex-auto-review", StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_carries_display_name_source_and_its_place_from_priority()
    {
        var mini = CodexModels.Read(CodexFixture).Single(m => string.Equals(m.Slug, "gpt-5.4-mini", StringComparison.Ordinal));

        Assert.Equal(new ModelFacts("gpt-5.4-mini", "GPT-5.4-Mini", ModelSource.Discovered, Order: 2), mini);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"models\":null}")]
    public void Codex_reader_returns_nothing_for_text_that_is_not_the_catalog(string text) =>
        Assert.Empty(CodexModels.Read(text));

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_skips_a_row_without_a_slug_and_treats_missing_visibility_as_hidden() =>
        Assert.Equal(["a"], CodexModels.Read("{\"models\":[{\"visibility\":\"list\"},{\"slug\":\"a\",\"visibility\":\"list\"},{\"slug\":\"b\"}]}").Select(m => m.Slug), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Claude_catalog_lists_the_aliases_cheapest_first() =>
        Assert.Equal(["haiku", "sonnet", "opus"], ClaudeModels.Catalog.Select(m => m.Slug), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_puts_gpt_5_2_cheapest_and_gpt_5_5_strongest()
    {
        var models = CodexModels.Read(CodexFixture);

        Assert.Equal(0, models.Single(m => string.Equals(m.Slug, "gpt-5.2", StringComparison.Ordinal)).Order);
        Assert.Equal(4, models.Single(m => string.Equals(m.Slug, "gpt-5.5", StringComparison.Ordinal)).Order);
    }

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_puts_a_mini_before_its_sibling_even_when_priority_says_otherwise()
    {
        const string json = "{\"models\":[{\"slug\":\"x\",\"visibility\":\"list\",\"priority\":9},{\"slug\":\"x-mini\",\"visibility\":\"list\",\"priority\":1}]}";

        var models = CodexModels.Read(json);

        Assert.True(models.Single(m => string.Equals(m.Slug, "x-mini", StringComparison.Ordinal)).Order < models.Single(m => string.Equals(m.Slug, "x", StringComparison.Ordinal)).Order);
    }

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_leaves_the_place_empty_for_a_model_with_no_priority() =>
        Assert.Null(CodexModels.Read("{\"models\":[{\"slug\":\"a\",\"visibility\":\"list\"}]}").Single().Order);

    [Fact, Trait("Lane", "unit")]
    public void Claude_catalog_places_haiku_sonnet_opus_at_0_1_2() =>
        Assert.Equal([0, 1, 2], ClaudeModels.Catalog.Select(m => m.Order!.Value));
}
