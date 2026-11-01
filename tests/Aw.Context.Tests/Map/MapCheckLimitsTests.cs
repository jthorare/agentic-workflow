using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.Map;

/// <summary>'context.checks.*' (owner decision for #1402): the structure checks' limits come from the profile, the defaults are the old numbers, the notes print the configured limit.</summary>
public sealed class MapCheckLimitsTests
{
    private static Result<ContextSettings> Read(string json)
    {
        using var section = System.Text.Json.JsonDocument.Parse(json);
        return SettingsReader.Read(section.RootElement);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_defaults_are_the_numbers_measured_on_real_maps()
    {
        var limits = Read("{}").Value.CheckLimits;

        Assert.Equal((5, 20, 5, 60, 2, 10), (limits.MinFilesIsolated, limits.MinFeaturesIsolated, limits.IsolatedDegradedPercent, limits.IsolatedRefusePercent, limits.OverCapDegradedPercent, limits.OverCapRefusePercent));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_graph_passes_under_one_setting_and_is_degraded_under_another()
    {
        var graph = StructureGraphs.Isolated(total: 100, isolated: 40);
        var loose = Read("""{"checks":{"isolated":{"degradedPercent":50,"refusePercent":80}}}""").Value.CheckLimits;

        Assert.Equal(CheckVerdict.Degraded, MapChecks.Measure(graph).Isolated.Verdict);
        Assert.Equal(CheckVerdict.Pass, MapChecks.Measure(graph, loose).Isolated.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_stricter_refuse_limit_refuses_a_graph_the_defaults_only_degrade()
    {
        var graph = StructureGraphs.Oversized(total: 100, oversized: 5);
        var strict = Read("""{"checks":{"overCap":{"degradedPercent":1,"refusePercent":4}}}""").Value.CheckLimits;

        Assert.Equal(CheckVerdict.Degraded, MapChecks.Measure(graph).OverCap.Verdict);
        Assert.Equal(CheckVerdict.Refuse, MapChecks.Measure(graph, strict).OverCap.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_minimums_decide_which_features_are_measured_and_the_skip_note_names_them()
    {
        var graph = StructureGraphs.Isolated(total: 19, isolated: 19);
        var few = Read("""{"checks":{"isolated":{"minFeatures":10}}}""").Value.CheckLimits;

        Assert.Contains("fewer than 20", MapChecks.Measure(graph).Isolated.Skipped, StringComparison.Ordinal);
        Assert.Null(MapChecks.Measure(graph, few).Isolated.Skipped);
        Assert.Contains("fewer than 30 features of at least 5 files", MapChecks.Measure(graph, new MapCheckLimits { MinFeaturesIsolated = 30 }).Isolated.Skipped, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_degraded_note_and_the_stored_reason_print_the_configured_limit()
    {
        var limits = Read("""{"checks":{"isolated":{"degradedPercent":35,"refusePercent":90}}}""").Value.CheckLimits;

        var outcome = MapChecks.Measure(StructureGraphs.Isolated(total: 100, isolated: 41), limits).Decide(ChecksMode.Refuse);

        Assert.Contains("limit 35%", outcome.Notes.Single(), StringComparison.Ordinal);
        Assert.Contains("limit 35%", outcome.Stored, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"checks":{"isolated":{"degradedPercent":101}}}""", "context.checks.isolated.degradedPercent")]
    [InlineData("""{"checks":{"isolated":{"refusePercent":-1}}}""", "context.checks.isolated.refusePercent")]
    [InlineData("""{"checks":{"isolated":{"minFiles":0}}}""", "context.checks.isolated.minFiles")]
    [InlineData("""{"checks":{"isolated":{"minFeatures":0}}}""", "context.checks.isolated.minFeatures")]
    [InlineData("""{"checks":{"overCap":{"degradedPercent":11}}}""", "context.checks.overCap.degradedPercent")]
    [InlineData("""{"checks":{"isolated":{"degradedPercent":70}}}""", "context.checks.isolated.degradedPercent")]
    [InlineData("""{"checks":{"overCap":{"refusePercent":101}}}""", "context.checks.overCap.refusePercent")]
    public void A_value_out_of_range_or_a_degraded_limit_above_the_refuse_limit_is_refused_with_its_key_path(string json, string path)
    {
        var read = Read(json);

        Assert.Equal("SND-CONTEXT-001", read.Error.Code);
        Assert.Contains(path, read.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_key_under_checks_is_refused() =>
        Assert.True(Read("""{"checks":{"isolated":{"percent":5}}}""").IsError);
}
