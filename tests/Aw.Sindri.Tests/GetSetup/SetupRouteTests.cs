using System.Text.Json;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.GetSetup;

/// <summary>GET /sindri/setup served by the real host with the real sources: eight steps in their fixed order, a read a session may make, and the profile step moving once the human approves.</summary>
public sealed class SetupRouteTests
{
    private static readonly string[] _order = ["serve", "sources", "repo", "profile", "agent", "mcp", "map", "first-workflow"];

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_route_answers_the_eight_steps_in_order_and_the_profile_waits_for_a_first_repo()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);

        var before = Steps(await Read(host));
        await ViewScenarios.Approve(host, TestContext.Current.CancellationToken);
        var after = Steps(await Read(host));

        Assert.Equal(_order, before.Select(static s => s.Id), StringComparer.Ordinal);
        Assert.Equal(_order, after.Select(static s => s.Id), StringComparer.Ordinal);
        // the served profile names no repo, so its step waits for a first repo before and after the approval, and the agent registration waits for the approved profile
        Assert.Equal(("needs-you", "AddRepo", "todo", null), (before[2].State, before[2].Action, before[3].State, before[3].Action));
        Assert.Equal(("todo", null), (before[5].State, before[5].Action));
        Assert.Equal(("todo", null), (after[3].State, after[3].Action));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_session_may_read_the_setup()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);

        var answer = await host.Http.Ask(Get("/sindri/setup", [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")]));

        Assert.Equal(System.Net.HttpStatusCode.OK, answer.Status);
    }

    private static async Task<string> Read(GoldenHost host) => (await host.Http.Ask(Get("/sindri/setup", Human(host.Token.Value)))).Body;

    private static List<(string Id, string State, string? Action)> Steps(string body)
    {
        using var document = JsonDocument.Parse(body);
        return [.. document.RootElement.GetProperty("steps").EnumerateArray()
            .Select(static s => (s.GetProperty("id").GetString() ?? string.Empty, s.GetProperty("state").GetString() ?? string.Empty, s.GetProperty("action").GetString()))];
    }
}
