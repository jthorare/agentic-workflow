using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Resources;

/// <summary>
/// Activating a workflow whose <c lang="text">resources.sandbox.memory</c> is above the profile's <c lang="text">sandbox.limits.memory</c>, on the real host's service container (#2531).
/// The checker that activate uses builds the box cap from the live profile; a host that drops it lets the workflow activate, and this test fails then.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
[Lane(TestLane.Integration)]
public sealed class ActivateRefusesSandboxAboveBoxCapOnTheRealHostTests
{
    private const string Profile = GoldenHost.ProfileYaml + "sandbox:\n  limits: { memory: 8GiB }\n";

    private static async Task<(HttpStatusCode Status, string Body)> Activate(string memory, CancellationToken ct)
    {
        await using var host = await GoldenHost.Start(temporary: true, ct, Profile);
        var id = WorkflowId.From("WF-00000000000000000000000000000003");
        var spec = new GraphSpec(Version: 1, Name: "Greedy", Description: null, Concurrency: null,
            Resources: new GraphResources(Database: null, new GraphSandbox(Memory: memory)),
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await host.App.Services.GetRequiredService<IWorkflowStore>().Create(id, WorkflowSlug.From("greedy"), spec, "owner", ct)).Value;

        var answer = await host.Http.Ask(Post("/sindri/workflows/greedy/activate", json: null, Human(host.Token.Value)));
        return (answer.Status, answer.Body);
    }

    [Fact(Timeout = 60_000)]
    public async Task Activate_refuses_a_sandbox_memory_above_the_profile_limit_with_SND_RES_005_naming_the_cap()
    {
        var (status, body) = await Activate("16GiB", TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, status);
        Assert.Contains("SND-RES-005", body, StringComparison.Ordinal);
        Assert.Contains("8GiB", body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    public async Task Activate_accepts_a_sandbox_memory_within_the_profile_limit()
    {
        var (status, body) = await Activate("4GiB", TestContext.Current.CancellationToken);

        Assert.True(status == HttpStatusCode.OK, body);
    }
}
