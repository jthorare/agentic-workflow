using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.Core;

public sealed class SettingsReaderTests
{
    private static Result<ContextSettings> Read(string json) => SettingsReader.Read(JsonDocument.Parse(json).RootElement.Clone());

    [Fact, Trait("Lane", "unit")]
    public void No_section_gives_the_spec_defaults()
    {
        var settings = SettingsReader.Read(section: null).Value;

        Assert.Equal((TimeSpan.FromSeconds(10), 200_000, 3, false), (settings.Deadline, settings.MaxBudget, settings.MinHits, settings.SessionsEnabled));
        Assert.Equal(ContextSettings.DefaultDocGlobs, settings.DocGlobs);
        Assert.Null(settings.Repos);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, 2)]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    public void The_cascade_depth_defaults_to_2_and_takes_0_to_4(int? depth, int expected)
    {
        var settings = depth is null ? SettingsReader.Read(section: null) : Read("{\"cascadeDepth\":" + depth.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");

        Assert.Equal(expected, settings.Value.CascadeDepth);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cascade_depth_over_8_is_SND_CONTEXT_001_at_its_key()
    {
        var settings = Read("""{"cascadeDepth":9}""");

        Assert.StartsWith("context.cascadeDepth:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, 50)]
    [InlineData(1, 1)]
    [InlineData(500, 500)]
    public void The_component_fan_out_defaults_to_50_and_takes_1_to_500(int? fanOut, int expected)
    {
        var settings = fanOut is null ? SettingsReader.Read(section: null) : Read("{\"cascadeComponentFanOut\":" + fanOut.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");

        Assert.Equal(expected, settings.Value.CascadeComponentFanOut);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(501)]
    public void A_component_fan_out_outside_1_to_500_is_SND_CONTEXT_001_at_its_key(int fanOut)
    {
        var settings = Read("{\"cascadeComponentFanOut\":" + fanOut.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");

        Assert.Equal("SND-CONTEXT-001", settings.Error.Code);
        Assert.StartsWith("context.cascadeComponentFanOut:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_key_is_read()
    {
        var settings = Read("""{"repos":["split-panes"],"docs":{"globs":["docs/**/*.md"]},"deadline":"500ms","maxBudget":6000,"minHits":1,"sessions":{"enabled":true}}""").Value;

        Assert.Equal((TimeSpan.FromMilliseconds(500), 6_000, 1, true), (settings.Deadline, settings.MaxBudget, settings.MinHits, settings.SessionsEnabled));
        Assert.Equal(["docs/**/*.md"], settings.DocGlobs, StringComparer.Ordinal);
        Assert.Equal(["split-panes"], settings.Repos!.Select(r => r.Value), StringComparer.Ordinal);   // why: set above
    }

    [Fact, Trait("Lane", "unit")]
    public void Extra_scrub_patterns_are_read_as_written()
    {
        var settings = Read("""{"scrub":{"extraPatterns":[{"id":"acme-customer-id","pattern":"ACME-[0-9]{6}"},{"id":"order-id"}]}}""").Value;

        Assert.Equal([new("acme-customer-id", "ACME-[0-9]{6}"), new("order-id", Pattern: null)], settings.ExtraPatterns);
        Assert.Empty(SettingsReader.Read(section: null).Value.ExtraPatterns);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"scrub":{"extraPatterns":[{"id":"a","pattern":"b"},null]}}""", "context.scrub.extraPatterns[1]:")]
    [InlineData("""{"scrub":{"extra":[]}}""", "context:")]
    [InlineData("""{"scrub":{"extraPatterns":[{"id":"a","pattern":"b","flags":"i"}]}}""", "context:")]
    [InlineData("""{"deadline":"soon"}""", "context.deadline:")]
    [InlineData("""{"deadline":"301s"}""", "context.deadline:")]
    [InlineData("""{"maxBudget":10}""", "context.maxBudget:")]
    [InlineData("""{"minHits":99}""", "context.minHits:")]
    [InlineData("""{"docs":{"globs":["../etc/*"]}}""", "context.docs.globs:")]
    [InlineData("""{"docs":{"globs":[]}}""", "context.docs.globs:")]
    [InlineData("""{"repos":[""]}""", "context.repos:")]
    [InlineData("""{"repos":[null]}""", "context.repos:")]
    [InlineData("""{"repos":["a/b"]}""", "context.repos:")]
    [InlineData("""{"docs":{"globs":[null]}}""", "context.docs.globs:")]
    [InlineData("""{"rerank":{"enabled":true}}""", "context.rerank:")]
    [InlineData("""{"sessions":{"enabled":true,"x":1}}""", "context:")]
    [InlineData("""[1]""", "context:")]
    public void A_bad_key_is_001_with_its_key_path(string json, string path)
    {
        var read = Read(json);

        Assert.Equal(ContextErrors.SettingsInvalid.Code, read.Error.Code);
        Assert.StartsWith(path, read.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Labeling_is_off_with_a_400000_token_cap_unless_the_profile_says_otherwise()
    {
        var settings = SettingsReader.Read(section: null).Value;

        Assert.Equal((false, 400_000), (settings.Labeling.Enabled, settings.Labeling.MaxTokens));
        Assert.Equal((false, 400_000), (Read("""{"sessions":{"enabled":true}}""").Value.Labeling.Enabled, Read("{}").Value.Labeling.MaxTokens));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_labeling_keys_are_read()
    {
        var settings = Read("""{"labeling":{"enabled":true,"maxTokens":50000}}""").Value;

        Assert.Equal((true, 50_000), (settings.Labeling.Enabled, settings.Labeling.MaxTokens));
        Assert.True(Read("""{"labeling":{"enabled":true}}""").Value.Labeling.Enabled);
        Assert.Equal(60_000, Read("""{"labeling":{"maxTokens":60000}}""").Value.Labeling.MaxTokens);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"labeling":{"maxTokens":999}}""")]
    [InlineData("""{"labeling":{"maxTokens":20000001}}""")]
    public void A_labeling_cap_outside_1000_to_20000000_is_SND_CONTEXT_001_at_its_key(string json)
    {
        var settings = Read(json);

        Assert.Equal("SND-CONTEXT-001", settings.Error.Code);
        Assert.StartsWith("context.labeling.maxTokens:", settings.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"labeling":{"enabled":"yes"}}""")]
    [InlineData("""{"labeling":{"enable":true}}""")]
    [InlineData("""{"labeling":true}""")]
    public void A_labeling_section_that_is_not_enabled_and_maxTokens_is_SND_CONTEXT_001(string json) =>
        Assert.Equal("SND-CONTEXT-001", Read(json).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void The_per_sync_labeling_cap_defaults_to_50000_and_is_read()
    {
        Assert.Equal(50_000, Read("{}").Value.Labeling.MaxTokensPerSync);
        Assert.Equal(20_000, Read("""{"labeling":{"maxTokensPerSync":20000}}""").Value.Labeling.MaxTokensPerSync);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"labeling":{"maxTokensPerSync":0}}""")]
    [InlineData("""{"labeling":{"maxTokensPerSync":-5}}""")]
    [InlineData("""{"labeling":{"maxTokensPerSync":20000001}}""")]
    public void A_per_sync_cap_outside_1_to_20000000_is_SND_CONTEXT_001_at_its_key(string json)
    {
        var settings = Read(json);

        Assert.Equal("SND-CONTEXT-001", settings.Error.Code);
        Assert.StartsWith("context.labeling.maxTokensPerSync:", settings.Error.Message, StringComparison.Ordinal);
    }
}
