using System.Text.Json.Nodes;
using Aw.Sindri.Testing.Line;

namespace Aw.Sindri.Tests.Plan;

/// <summary>PLN-TEST-001 on the plan station's checks (PlanRules.All), as the line's plan checks have it (#994, #1054): a phase that allows an existing test file names it in its test-change.</summary>
public sealed class PlanTestNamedTests
{
    private static JsonNode Task(JsonNode n, string id) => n["tasks"]!.AsArray().Single(t => (string?)t!["id"] == id)!;

    private static async Task<string[]> Failed(LineFixture facts) =>
        [.. (await PlanCheckTests.Run(facts)).Findings.Where(static f => f.Rule == "PLN-TEST-001").Select(static f => f.Message)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_that_allows_an_existing_test_file_must_name_it_in_its_test_change()
    {
        var unnamed = LineFixture.Plan().Edit("tasks.json", n => Task(n, "P1")["writeScope"] = JsonNode.Parse("""["scripts/x.sh","scripts/tests/x.test.sh"]"""));
        var named = LineFixture.Plan().Edit("tasks.json", n =>
        {
            Task(n, "P1")["writeScope"] = JsonNode.Parse("""["scripts/x.sh","scripts/tests/x.test.sh"]""");
            Task(n, "P1")["rules"]!.AsArray().Add("test-change:scripts/tests/x.test.sh:new cases");
        });
        var created = LineFixture.Plan().Edit("tasks.json", n => Task(n, "P1")["writeScope"] = JsonNode.Parse("""["scripts/x.sh","new: scripts/tests/y.test.sh","scripts/**"]"""));

        var findings = await Failed(unnamed);

        Assert.Contains("scripts/tests/x.test.sh", Assert.Single(findings), StringComparison.Ordinal);
        Assert.Empty(await Failed(named));
        Assert.Empty(await Failed(created));   // a file the phase creates, or a glob, is not an existing test the plan must name
    }
}
