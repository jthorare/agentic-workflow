using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Interpolation;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Tests.Workflow.Code;

namespace Aw.Sindri.Tests.Graph.Ops;

/// <summary>The 'trim' step of the 'style-rule-proposer' starter (issue 3787): what it keeps for 'vet' and 'classify', and that their stored inputs stay under the engine's 64 KB cap.</summary>
[Lane(TestLane.Unit)]
public sealed class StyleRuleProposerTrimTests : IClassFixture<RoslynWarmup>
{
    private static readonly string[] _sections = ["4b.1", "4b.1a", "4b.2", "4b.3", "4b.4", "4b.5", "4b.6", "4b.8", "4b.9"];

    /// <summary>Takes the warm-up so xUnit builds it before the first fact; the fixture's work is done in its constructor.</summary>
    /// <param name="warmup">The warm-up, already run.</param>
    public StyleRuleProposerTrimTests(RoslynWarmup warmup)
    {
        ArgumentNullException.ThrowIfNull(warmup);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GraphSpec Spec => GraphTemplates.Find("style-rule-proposer")!.Spec;

    [Fact]
    public void Trim_keeps_id_section_and_checks_for_vet_and_the_cut_text_for_classify_and_drops_the_rest()
    {
        var rule = new JsonObject
        {
            ["id"] = "engineering.csharp.a-rule",
            ["kind"] = "engineering",
            ["section"] = "4b.2",
            ["text"] = new string('t', 600),
            ["status"] = "active",
            ["checks"] = new JsonArray(new JsonObject { ["type"] = "analyzer", ["id"] = "CA1305" }),
        };

        var trimmed = Trim(new JsonArray(rule));

        var known = trimmed["known"]!.AsArray().Single()!.AsObject();
        Assert.Equal(["checks", "id", "section"], known.Select(static p => p.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(("engineering.csharp.a-rule", "4b.2", "CA1305"), (known["id"]!.GetValue<string>(), known["section"]!.GetValue<string>(), known["checks"]![0]!["id"]!.GetValue<string>()));
        var brief = trimmed["brief"]!.AsArray().Single()!.AsObject();
        Assert.Equal(["id", "section", "text"], brief.Select(static p => p.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(StyleRuleProposer.BriefChars, brief["text"]!.GetValue<string>().Length);
    }

    [Fact]
    public void Trim_never_splits_a_surrogate_pair_and_skips_a_record_with_no_id()
    {
        var emoji = new JsonObject { ["id"] = "engineering.csharp.emoji", ["section"] = "4b.1", ["text"] = new string('z', StyleRuleProposer.BriefChars - 1) + "😀" + "tail" };
        var nameless = new JsonObject { ["section"] = "4b.1", ["text"] = "no id" };

        var trimmed = Trim(new JsonArray(emoji, nameless));

        Assert.Single(trimmed["known"]!.AsArray());
        Assert.Equal(new string('z', StyleRuleProposer.BriefChars - 1), trimmed["brief"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Trim_keeps_vet_and_classify_inputs_under_the_64_KB_cap_for_200_records()
    {
        var rules = new JsonArray();
        for (var i = 0; i < 200; i++)
        {
            var name = i.ToString("D3", CultureInfo.InvariantCulture);
            rules.Add((JsonNode)new JsonObject
            {
                ["id"] = "engineering.csharp." + name + new string('i', 37),
                ["kind"] = "engineering",
                ["section"] = _sections[i % _sections.Length],
                ["text"] = new string('a', 600),
                ["status"] = "active",
                ["checks"] = new JsonArray(Check("analyzer", "CA" + name), Check("test", "Aw" + new string('T', 30) + name), Check("review", new string('r', 40) + name)),
            });
        }

        var trimmed = Trim(rules);
        var scope = new JsonObject
        {
            ["trim"] = new JsonObject { ["result"] = trimmed },
            ["rules"] = new JsonObject { ["sections"] = new JsonArray([.. _sections.Select(static s => (JsonNode)s)]) },
            ["classify"] = new JsonObject { ["answer"] = new JsonObject { ["kind"] = "analyzer", ["checkId"] = "CA1822", ["slug"] = new string('s', 60), ["section"] = "4b.1a", ["text"] = new string('x', 600), ["why"] = new string('y', 500), ["duplicateOf"] = string.Empty } },
            ["item"] = new JsonObject { ["label"] = "label", ["mistake"] = new string('m', 300) },
        };

        var vet = Stored("vet", "input", scope);
        var classify = Stored("classify", "prompt", scope);

        Assert.True(vet < NodeRunLimits.PayloadBytes, "vet's stored input is " + vet.ToString(CultureInfo.InvariantCulture) + " bytes");
        Assert.True(classify < NodeRunLimits.PayloadBytes, "classify's stored input is " + classify.ToString(CultureInfo.InvariantCulture) + " bytes");
    }

    [Fact]
    public void Trim_keeps_its_output_under_the_64_KB_cap_for_200_worst_case_records_and_counts_what_it_leaves_out()
    {
        var rules = new JsonArray();
        for (var i = 0; i < 200; i++)
        {
            var name = i.ToString("D3", CultureInfo.InvariantCulture);
            rules.Add((JsonNode)new JsonObject
            {
                ["id"] = "engineering.csharp." + name + new string('i', 77),
                ["section"] = "4b.1a",
                ["text"] = string.Concat(Enumerable.Repeat("\"\\é😀", 150)),
                ["checks"] = new JsonArray(Check("analyzer", "CA" + name), Check("test", new string('T', 60) + name), Check("review", new string('r', 80) + name), Check("test", new string('q', 80) + name)),
            });
        }

        var trimmed = Trim(rules);

        var bytes = Encoding.UTF8.GetByteCount(new JsonObject { ["result"] = trimmed.DeepClone() }.ToJsonString());
        Assert.True(bytes < NodeRunLimits.PayloadBytes, "trim's output is " + bytes.ToString(CultureInfo.InvariantCulture) + " bytes");
        var kept = trimmed["known"]!.AsArray().Count;
        Assert.Equal(200, kept + trimmed["more"]!.GetValue<int>());
        Assert.Equal(trimmed["more"]!.GetValue<int>(), trimmed["trimmed"]!.GetValue<int>());
        Assert.InRange(kept, 1, 199);
        Assert.Equal(kept, trimmed["brief"]!.AsArray().Count);
    }

    [Fact]
    public void Trim_hands_on_every_record_of_a_catalog_the_size_of_today_s_and_says_none_were_left_out()
    {
        var rules = new JsonArray();
        for (var i = 0; i < 95; i++)
        {
            var name = i.ToString("D3", CultureInfo.InvariantCulture);
            rules.Add((JsonNode)new JsonObject { ["id"] = "engineering.csharp." + name + new string('i', 37), ["section"] = "4b.2", ["text"] = new string('a', 500), ["checks"] = new JsonArray(Check("test", "Aw" + new string('T', 30) + name), Check("review", new string('r', 40) + name)) });
        }

        var trimmed = Trim(rules);

        Assert.Equal((95, 0), (trimmed["known"]!.AsArray().Count, trimmed["more"]!.GetValue<int>()));
    }

    [Fact]
    public void Trim_adds_the_records_rule_catalog_left_out_to_its_own_drops_in_one_more()
    {
        var rules = new JsonArray();
        for (var i = 0; i < 300; i++)
        {
            rules.Add((JsonNode)new JsonObject { ["id"] = "engineering.csharp.r" + i.ToString("D3", CultureInfo.InvariantCulture) + new string('i', 37), ["section"] = "4b.2", ["text"] = new string('a', 500), ["checks"] = new JsonArray(Check("test", new string('T', 40))) });
        }

        var trimmed = Trim(rules, more: 7);

        var own = trimmed["trimmed"]!.GetValue<int>();
        Assert.InRange(own, 1, 299);
        Assert.Equal(300, trimmed["known"]!.AsArray().Count + own);
        Assert.Equal(7 + own, trimmed["more"]!.GetValue<int>());
    }

    private static JsonObject Check(string type, string id) => new() { ["type"] = type, ["id"] = id };

    // why: the engine stores a step's interpolated config through StoredJson.Fit; this is that config's size with the field rendered from the scope, as the caps tests of 'steering-miner' measure it
    private static int Stored(string nodeId, string field, JsonObject scope)
    {
        var config = (JsonObject)Spec.Nodes.Single(n => string.Equals(n.Id.Value, nodeId, StringComparison.Ordinal)).Config.DeepClone();
        var rendered = Template.Render(config[field]!.GetValue<string>(), reference => Lookup(scope, reference));
        config[field] = rendered is JsonValue text && text.TryGetValue<string>(out var plain) ? plain : rendered?.ToJsonString();
        return Encoding.UTF8.GetByteCount(config.ToJsonString());
    }

    private static JsonObject Trim(JsonArray rules, int more = 0)
    {
        var config = Spec.Nodes.Single(static n => string.Equals(n.Id.Value, "trim", StringComparison.Ordinal)).Config;
        var scope = new JsonObject { ["rules"] = new JsonObject { ["rules"] = rules, ["more"] = more } };
        var input = Template.Render(config["input"]!.GetValue<string>(), reference => Lookup(scope, reference));
        Result<string> result = InProcessCodePort.Execute(config["code"]!.GetValue<string>(), input?.ToJsonString() ?? "null", Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return (JsonObject)JsonNode.Parse(result.Value)!;
    }

    private static JsonNode? Lookup(JsonObject scope, string reference)
    {
        JsonNode? node = scope;
        foreach (var part in reference.Split('.'))
        {
            node = node is JsonObject map ? map[part] : null;
        }

        return node;
    }
}
