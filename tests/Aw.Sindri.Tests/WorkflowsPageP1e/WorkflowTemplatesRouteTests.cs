using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Mcp;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.WorkflowsPageP1e;

/// <summary>GET /sindri/workflows/templates (Workflows page spec E7, #3193): templates A, B and C with trigger, step count, needs and last run.</summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowTemplatesRouteTests
{
    [Fact(Timeout = 60_000)]
    public async Task The_templates_route_answers_A_B_and_C_with_the_fields_the_start_screen_shows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, SeededMcp.Profile);

        var answer = await host.Http.Ask(Get("/sindri/workflows/templates", Human(host.Token.Value)));

        Assert.Equal(System.Net.HttpStatusCode.OK, answer.Status);
        var rows = JsonDocument.Parse(answer.Body).RootElement.GetProperty("templates").EnumerateArray().ToList();
        Assert.Equal(["A", "B", "C"], rows.Select(static r => r.GetProperty("id").GetString()), StringComparer.Ordinal);
        Assert.All(rows, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("title").GetString()));
            Assert.True(r.GetProperty("steps").GetInt32() >= 2);
            Assert.Equal("connection:github", Assert.Single(r.GetProperty("needs").EnumerateArray()).GetString());
            Assert.Equal(JsonValueKind.Null, r.GetProperty("lastRunAt").ValueKind);
        });
    }
}
