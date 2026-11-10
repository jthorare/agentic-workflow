using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Resources;

/// <summary>
/// Activating a workflow that declares <c lang="text">resources.database</c> on the real host's service container (the golden host). The activate service and the node
/// activities take the provisioner as an optional parameter, so a dropped registration would turn resources off and every test with a hand-built service
/// would stay green; this one fails then.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
[Lane(TestLane.Integration)]
public sealed class ActivateProvisionsOnTheRealHostTests
{
    [Fact(Timeout = 60_000)]
    public async Task Activate_makes_the_declared_database_file_and_books_its_row()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct);
        var id = WorkflowId.From("WF-00000000000000000000000000000002");
        var slug = WorkflowSlug.From("resourceful");
        var spec = new GraphSpec(Version: 1, Name: "Resourceful", Description: null, Concurrency: null,
            Resources: new GraphResources(new GraphDatabase("state"), Sandbox: null),
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await host.App.Services.GetRequiredService<IWorkflowStore>().Create(id, slug, spec, "owner", ct)).Value;

        var answer = await host.Http.Ask(Post("/sindri/workflows/resourceful/activate", json: null, Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var db = Path.Combine(host.Home, "resources", "workflow", "resourceful", "state.db");
        Assert.True(File.Exists(db), db);
        var owner = ResourceOwner.Parse("workflow:resourceful").Value;
        var row = Assert.Single((await host.App.Services.GetRequiredService<ResourceProvisioner>().Booked(owner, ct)).Value);
        Assert.Equal(("database", "state", db), (row.Kind, row.Name, row.Path));
    }
}
