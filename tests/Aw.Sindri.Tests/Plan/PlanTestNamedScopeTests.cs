using System.Text.Json.Nodes;
using Aw.Sindri.Testing.Line;
using LinePlanChecks = Aw.Sindri.Features.Line.PlanChecks;

namespace Aw.Sindri.Tests.Plan;

/// <summary>PLN-TEST-001 reads a phase's write scope, or its allowed files when the write scope is empty, on both paths (#1054): the plan station's and the line's.</summary>
public sealed class PlanTestNamedScopeTests
{
    private const string Allowed = """["scripts/x.sh","scripts/tests/x.test.sh"]""";
    private const string Design = """{"acceptance":{"items":[{"id":"AC-1","text":"a"}]},"signals":[{"item":"AC-1","kind":"api-transcript","statement":"returns 410","runs":"head"}]}""";
    private const string LinePlan = """{"phases":[{"id":"P1","title":"first","workstream":"api","deps":[],"size":"S","kind":"feature","acceptance":[{"id":"AC-1","evidence":["api-transcript"]}]}],"execReview":[{"phase":"P1","verdict":"executable"}]}""";

    private static JsonNode Task(JsonNode n, string id) => n["tasks"]!.AsArray().Single(t => (string?)t!["id"] == id)!;

    private static async Task<int> PlanPathFindings(Action<JsonNode> edit)
    {
        var facts = LineFixture.Plan().Edit("tasks.json", edit);
        return (await PlanCheckTests.Run(facts)).Findings.Count(static f => f.Rule == "PLN-TEST-001");
    }

    private static int LinePathFindings(Action<JsonObject> edit)
    {
        var design = (JsonObject)JsonNode.Parse(Design)!;
        var plan = (JsonObject)JsonNode.Parse(LinePlan)!;
        edit(plan);
        return LinePlanChecks.Run(design, plan, holdoutSets: null, evidence: false).Failures.Count(static f => f.Rule == "PLN-TEST-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_plan_station_reads_allowed_files_when_the_write_scope_is_empty()
    {
        Assert.Equal(1, await PlanPathFindings(n =>
        {
            Task(n, "P1")["writeScope"] = new JsonArray();   // the gold phase carries a write scope, which would win over allowedFiles
            Task(n, "P1")["allowedFiles"] = JsonNode.Parse(Allowed);
        }));
        Assert.Equal(0, await PlanPathFindings(n =>
        {
            Task(n, "P1")["writeScope"] = new JsonArray();
            Task(n, "P1")["allowedFiles"] = JsonNode.Parse(Allowed);
            Task(n, "P1")["rules"]!.AsArray().Add("test-change:scripts/tests/x.test.sh:new cases");
        }));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_line_path_reads_allowed_files_when_the_write_scope_is_empty()
    {
        Assert.Equal(1, LinePathFindings(p => p["phases"]![0]!["allowedFiles"] = JsonNode.Parse(Allowed)));
        Assert.Equal(0, LinePathFindings(p =>
        {
            p["phases"]![0]!["allowedFiles"] = JsonNode.Parse(Allowed);
            p["phases"]![0]!["testChange"] = JsonNode.Parse("""[{"path":"scripts/tests/x.test.sh","reason":"new cases"}]""");
        }));
    }
}
