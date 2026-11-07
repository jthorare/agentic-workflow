using Aw.Kernel.Testing;
using Aw.Sindri.Core.Skills;

namespace Aw.Sindri.Tests.Skills;

/// <summary>One bad block.yaml never breaks the load, and the aw-result fence is read the way scripts/check-skill-blocks.mjs reads it (skill flows spec §3.1).</summary>
[Lane(TestLane.Unit)]
public sealed class SkillManifestParserTests
{
    private const string Good = """{"id":"good","sentence":"Does a thing.","outcomes":{"success":"done"},"maxDuration":"1h"}""";
    private const string Fenced = "# Skill\n\n## Result block\n\n```aw-result\n{\"skill\":\"x\"}\n```\n";

    private static SkillBlocks Parse(string manifestJson, string? markdown = null) => SkillManifestParser.Parse("x", "/s/x/block.yaml", manifestJson, markdown);

    [Fact]
    public void A_fractional_schema_skips_the_file_with_a_reason_instead_of_throwing()
    {
        var blocks = Parse("""{"schema":1.5,"blocks":[]}""");

        Assert.Empty(blocks.Loaded);
        Assert.Contains("schema: 1", Assert.Single(blocks.Skipped).Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"id":"bad","sentence":"x","maxDuration":"1h","outcomes":{"success":"done","from":"v","map":{"A":5}}}""")]
    [InlineData("""{"id":"bad","sentence":"x","maxDuration":"1h","outcomes":{"success":"done"},"args":[5]}""")]
    public void A_bad_block_is_skipped_with_a_reason_and_the_good_one_beside_it_loads(string bad)
    {
        var blocks = Parse($$"""{"schema":1,"blocks":[{{bad}},{{Good}}]}""");

        Assert.Equal("good", Assert.Single(blocks.Loaded).Manifest.Name);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(blocks.Skipped).Reason));
    }

    [Fact]
    public void An_aw_result_artifacts_list_with_a_non_string_skips_the_block_not_the_load()
    {
        var markdown = "## Result block\n\n```aw-result\n{\"skill\":\"x\",\"artifacts\":[5]}\n```\n";

        var blocks = Parse($$"""{"schema":1,"blocks":[{{Good}}]}""", markdown);

        _ = Assert.Single(blocks.Loaded);
    }

    [Fact]
    public void One_aw_result_fence_in_the_Result_block_section_loads() =>
        _ = Assert.Single(Parse($$"""{"schema":1,"blocks":[{{Good}}]}""", Fenced).Loaded);

    [Fact]
    public void An_aw_result_fence_outside_the_Result_block_section_does_not_count()
    {
        var markdown = "# Skill\n\n```aw-result\n{\"skill\":\"x\"}\n```\n\n## Result block\n\nnothing here\n";

        var skipped = Assert.Single(Parse($$"""{"schema":1,"blocks":[{{Good}}]}""", markdown).Skipped);

        Assert.Contains("aw-result", skipped.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_aw_result_fences_skip_the_block_and_say_so()
    {
        var markdown = Fenced + "\n```aw-result\n{\"skill\":\"x\"}\n```\n";

        var blocks = Parse($$"""{"schema":1,"blocks":[{{Good}}]}""", markdown);

        Assert.Empty(blocks.Loaded);
        Assert.Contains("2 aw-result", Assert.Single(blocks.Skipped).Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"id":"","sentence":"x","maxDuration":"1h","outcomes":{"success":"done"}}""", "kebab-case")]
    [InlineData("""{"id":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","sentence":"x","maxDuration":"1h","outcomes":{"success":"done"}}""", "id")]
    [InlineData("""{"id":"bad","sentence":"x","maxDuration":"1h","outcomes":{"success":"Not-Camel"}}""", "outcome")]
    [InlineData("""{"id":"bad","sentence":"x","maxDuration":"1h","outcomes":{"success":"done","from":"v","map":{"A":"done","B":"Not-Camel"}}}""", "outcome")]
    public void A_value_the_descriptor_rejects_skips_the_block_with_a_reason_and_the_good_one_loads(string bad, string named)
    {
        var blocks = Parse($$"""{"schema":1,"blocks":[{{bad}},{{Good}}]}""");

        Assert.Equal("good", Assert.Single(blocks.Loaded).Manifest.Name);
        Assert.Contains(named, Assert.Single(blocks.Skipped).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Building_a_descriptor_from_an_invalid_outcome_name_returns_an_error_instead_of_throwing()
    {
        var manifest = new SkillBlockManifest("bad", "x", Inputs: [], Args: [], Outputs: [], new SkillOutcomes(From: null, new Dictionary<string, string>(StringComparer.Ordinal), "Not-Camel"), Pushes: false, MaxDuration: TimeSpan.FromHours(1));

        Assert.True(SkillDescriptors.Build(manifest).IsError);
    }
}
