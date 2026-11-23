using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

/// <summary>#3759: a schedule's timezone or cron of the wrong type is a finding, not an absent value.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphValidatorTimezoneTests
{
    private static GraphValidation Check(JsonNode? timezone, JsonNode cron)
    {
        var node = new GraphNode(NodeId.From("daily"), "schedule", Title: null, Config: new JsonObject { ["cron"] = cron, ["timezone"] = timezone });
        return Validator().Validate(Spec([node], []), GraphMode.Activate);
    }

    public static TheoryData<string> BadTimezones => ["5", "\"\"", "[]", "true", "[\"America/Los_Angeles\"]"];

    [Theory]
    [MemberData(nameof(BadTimezones))]
    public void A_timezone_that_is_not_a_non_empty_string_is_refused(string json)
    {
        var result = Check(JsonNode.Parse(json), JsonValue.Create("0 7 * * *"));

        var finding = Assert.Single(result.Findings, static f => f.Code == "GRAPH-CRON-001");
        Assert.Equal("config.timezone", finding.Field);
        Assert.Contains("America/Los_Angeles", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void A_timezone_name_gives_no_finding()
        => Assert.DoesNotContain(Check(JsonValue.Create("America/Los_Angeles"), JsonValue.Create("0 7 * * *")).Findings, static f => f.Code == "GRAPH-CRON-001");

    [Theory]
    [InlineData("5")]
    [InlineData("[\"0 7 * * *\", 5]")]
    public void A_cron_entry_that_is_not_a_string_is_refused(string json)
    {
        var result = Check(JsonValue.Create("UTC"), JsonNode.Parse(json)!);

        var finding = Assert.Single(result.Findings, static f => f.Code == "GRAPH-CRON-001");
        Assert.Equal("config.cron", finding.Field);
    }
}
