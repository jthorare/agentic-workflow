using System.Text;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The YAML a recipe can't use (spec §3.1), each RCP-SCHEMA-003 at the place it is written.</summary>
public sealed class RecipeYamlRefusalTests
{
    private const string Head = "schema: 1\nid: workflow.refused\nversion: 1\ninitial: produce\n";

    [Theory, Trait("Lane", "unit")]
    [InlineData("duplicate key", Head + "version: 2\n", "RCP-SCHEMA-003: key 'version' is written twice at test.yaml:5:1 (version); keep one 'version'")]
    [InlineData("anchor", Head + "description: &text \"a recipe\"\n", "RCP-SCHEMA-003: anchor '&text' isn't allowed at test.yaml:5:14 (description); write the value out in full; recipes don't use anchors or aliases")]
    [InlineData("alias", Head + "description: &text \"a recipe\"\nevals: [*text]\n", "RCP-SCHEMA-003: alias '*text' isn't allowed at test.yaml:6:9 (evals[0]); write the value out in full; recipes don't use anchors or aliases")]
    [InlineData("merge key", Head + "states:\n  produce:\n    <<: { kind: active }\n", "RCP-SCHEMA-003: merge key '<<' isn't allowed at test.yaml:7:5 (states.produce.<<); write the merged fields out in full")]
    [InlineData("explicit tag", Head + "description: !!str 12\n", "RCP-SCHEMA-003: explicit tag 'tag:yaml.org,2002:str' isn't allowed at test.yaml:5:14 (description); remove the tag; values are read with the YAML 1.2 core schema")]
    [InlineData("a key that isn't text", Head + "? [a, b]\n: c\n", "RCP-SCHEMA-003: a key must be plain text at test.yaml:5:3 (); write the key as text")]
    [InlineData("two documents", Head + "---\nschema: 1\n", "RCP-SCHEMA-003: the file holds more than one YAML document at test.yaml:5:1 (); keep one recipe per file")]
    [InlineData("invalid YAML", Head + "states: { produce: [\n", "RCP-SCHEMA-003: the file isn't valid YAML: While parsing a node, did not find expected node content. at test.yaml:6:1 (); fix the YAML at that position")]
    [InlineData("empty file", "# nothing here\n", "RCP-SCHEMA-003: the file holds no YAML document at test.yaml:1:1 (); write the recipe as one YAML mapping")]
    public void Refused_yaml_is_reported_where_it_is_written(string feature, string yaml, string line)
    {
        Assert.False(string.IsNullOrEmpty(feature));
        Assert.Contains(line, RecipeReaderTests.Lines(yaml), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("schema: 1\ninputs: [fact:item\ninitial: a\n")]
    [InlineData(Head + "states: { produce: { kind: active }\ntransitions: []\n")]
    public void An_unclosed_flow_collection_is_one_RCP_SCHEMA_003_not_a_crash(string yaml)
    {
        var lines = RecipeReaderTests.Lines(yaml);

        var line = Assert.Single(lines);
        Assert.StartsWith("RCP-SCHEMA-003: the file isn't valid YAML: ", line, StringComparison.Ordinal);
        Assert.EndsWith("; fix the YAML at that position", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Nesting_deeper_than_32_is_refused_once_at_the_first_level_too_deep()
    {
        var deep = new StringBuilder("evals: ");
        for (var i = 0; i < 40; i++)
        {
            _ = deep.Append('[');
        }

        var lines = RecipeReaderTests.Lines(Head + deep + new string(']', 40) + "\n");

        Assert.Contains(
            "RCP-SCHEMA-003: the recipe nests deeper than 32 levels at test.yaml:5:39 (evals[0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0][0]); flatten the structure",
            lines,
            StringComparer.Ordinal);
        _ = Assert.Single(lines, l => l.StartsWith("RCP-SCHEMA-003", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_over_4_MiB_is_refused_before_it_is_parsed()
    {
        var big = Head + "description: \"" + new string('x', YamlRecipeReader.MaxBytes) + "\"\n";

        Assert.Equal(
            ["RCP-SCHEMA-003: the file is 4194379 bytes, over the 4 MiB limit at test.yaml:1:1 (); split the recipe, or move long texts out of it"],
            RecipeReaderTests.Lines(big));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_refused_feature_in_one_file_is_reported()
    {
        var yaml = Head + "description: &text \"a recipe\"\nevals: [*text, !!str x]\nversion: 2\n";

        var refused = RecipeReaderTests.Lines(yaml).Where(l => l.StartsWith("RCP-SCHEMA-003", StringComparison.Ordinal)).ToList();

        Assert.Equal(4, refused.Count);
    }
}
