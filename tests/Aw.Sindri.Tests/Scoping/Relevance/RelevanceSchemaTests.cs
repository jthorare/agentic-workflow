using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Tests.Scoping.Relevance;

/// <summary>The four relevance schemas (spec 4.1 to 4.3) accept the pinned example shapes and refuse the ones that break a rule.</summary>
[Lane(TestLane.Unit)]
public sealed class RelevanceSchemaTests
{
    private const string Verdicts = """{"model":"claude-sonnet-5-5","verdicts":[{"id":"L-code-3","class":"noise","reason":"A shared admin shell that every feature imports.","citation":"L-code-3","fix":"ignore"},{"id":"L-product-2","class":"out","reason":"Billing export, not invitations.","citation":"R1","fix":"relabel","relabelTo":"feature:billing-export"},{"id":"L-code-1","class":"in","reason":"Writes the invitation expiry date.","citation":"L-code-1"}]}""";

    private const string Rulings = """{"judge":{"providers":["jev"],"threshold":0.6},"rulings":[{"id":"L-code-3","class":"noise","confidence":0.91,"provider":"jev","decisionId":"JD-0001","reasonCode":"model"},{"id":"L-code-1","class":"uncertain","confidence":null,"provider":null,"decisionId":"JD-0003","reasonCode":"below-threshold"}]}""";

    private const string Relevance = """{"agent":{"reportedModel":"claude-sonnet-5-5"},"judge":{"providers":["jev"]},"counts":{"reviewed":87,"in":64,"out":14,"noise":9,"disputed":3},"truncated":["code"],"entries":[{"id":"L-code-3","lens":"code","key":"code:src/admin/Shell.ts","kind":"code","label":"Shell.ts","agent":{"class":"noise","reason":"A shared admin shell.","citation":"L-code-3","fix":"ignore"},"judge":{"class":"noise","confidence":0.91,"provider":"jev","decisionId":"JD-0001","reasonCode":"model"},"result":"noise","disputed":false}]}""";

    private const string MapFixes = """{"fixes":[{"id":"L-code-3","node":"code:src/admin/Shell.ts","action":"ignore","relabelTo":null,"reason":"A shared admin shell.","citation":"L-code-3","status":"proposed"}]}""";

    private static IReadOnlyList<(string Path, string Message)> Check(string schema, string json, bool packetFile = false) => ScopeSchemas.Errors(schema, JsonNode.Parse(json), packetFile);

    private static string Without(string json, string path) => Edit(json, node => RemovePath(node, path));

    private static string Edit(string json, Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(json)!;
        edit(node);
        return node.ToJsonString();
    }

    private static void RemovePath(JsonNode root, string path)
    {
        var parts = path.Split('.');
        var node = root;
        foreach (var part in parts[..^1])
        {
            node = int.TryParse(part, out var index) ? node.AsArray()[index]! : node[part]!;
        }

        _ = node.AsObject().Remove(parts[^1]);
    }

    [Fact]
    public void The_pinned_example_shapes_are_valid()
    {
        Assert.Empty(Check("relevance-verdicts", Verdicts));
        Assert.Empty(Check("relevance-rulings", Rulings));
        Assert.Empty(Check("relevance", Relevance));
        Assert.Empty(Check("map-fixes", MapFixes));
    }

    [Fact]
    public void The_packet_file_forms_carry_a_schema_version()
    {
        Assert.Empty(Check("relevance", Edit(Relevance, static n => n["schemaVersion"] = 1)));
        Assert.NotEmpty(Check("relevance", Edit(Relevance, static n => n["schemaVersion"] = 2)));
    }

    [Fact]
    public void A_verdict_with_a_class_outside_in_out_noise_is_refused() =>
        Assert.NotEmpty(Check("relevance-verdicts", Edit(Verdicts, static n => n["verdicts"]![0]!["class"] = "maybe")));

    [Fact]
    public void A_verdict_that_leaves_the_map_needs_a_fix_and_one_that_stays_has_none()
    {
        Assert.NotEmpty(Check("relevance-verdicts", Without(Verdicts, "verdicts.0.fix")));
        Assert.NotEmpty(Check("relevance-verdicts", Edit(Verdicts, static n => n["verdicts"]![2]!["fix"] = "ignore")));
    }

    [Fact]
    public void A_relabel_names_where_to_and_the_other_fixes_do_not()
    {
        Assert.NotEmpty(Check("relevance-verdicts", Without(Verdicts, "verdicts.1.relabelTo")));
        Assert.NotEmpty(Check("relevance-verdicts", Edit(Verdicts, static n => n["verdicts"]![0]!["relabelTo"] = "feature:x")));
    }

    [Fact]
    public void The_verdicts_must_name_the_model_and_cite_every_entry()
    {
        Assert.NotEmpty(Check("relevance-verdicts", Without(Verdicts, "model")));
        Assert.NotEmpty(Check("relevance-verdicts", Without(Verdicts, "verdicts.0.citation")));
        Assert.NotEmpty(Check("relevance-verdicts", Edit(Verdicts, static n => n["verdicts"]![0]!["id"] = "entry-3")));
    }

    [Fact]
    public void A_ruling_class_is_in_out_noise_or_uncertain_and_keeps_all_its_fields()
    {
        Assert.NotEmpty(Check("relevance-rulings", Edit(Rulings, static n => n["rulings"]![0]!["class"] = "maybe")));
        Assert.NotEmpty(Check("relevance-rulings", Without(Rulings, "rulings.0.decisionId")));
        Assert.NotEmpty(Check("relevance-rulings", Without(Rulings, "judge.providers")));
    }

    [Fact]
    public void A_relevance_entry_result_is_in_out_or_noise_never_uncertain()
    {
        Assert.NotEmpty(Check("relevance", Edit(Relevance, static n => n["entries"]![0]!["result"] = "uncertain")));
        Assert.NotEmpty(Check("relevance", Without(Relevance, "counts.disputed")));
    }

    [Fact]
    public void A_map_fix_has_a_known_action_and_status()
    {
        Assert.NotEmpty(Check("map-fixes", Edit(MapFixes, static n => n["fixes"]![0]!["action"] = "delete")));
        Assert.NotEmpty(Check("map-fixes", Edit(MapFixes, static n => n["fixes"]![0]!["status"] = "done")));
        Assert.Empty(Check("map-fixes", Edit(MapFixes, static n => n["fixes"]![0]!["status"] = "recorded")));
    }
}
