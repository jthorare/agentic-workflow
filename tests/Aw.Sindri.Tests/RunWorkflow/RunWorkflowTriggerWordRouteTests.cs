using System.Net;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.RunWorkflow;

/// <summary>
/// A hand start through POST /sindri/workflows/{slug}/runs with the real caller headers lists its trigger by the client that sent it (#3683):
/// 'sindri wf run' (X-Sindri-Client: cli, actor 'human:cli') is "cli", the app's Run button (X-Sindri-Client: ui, actor 'human:ui') stays "manual".
/// The golden host runs no Temporal worker, so a starter that records the run the way the real one does stands in for it.
/// </summary>
public sealed class RunWorkflowTriggerWordRouteTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    [Theory(Timeout = 60_000), Trait("Lane", "integration")]
    [InlineData("cli", "cli")]
    [InlineData("ui", "manual")]
    public async Task A_hand_start_lists_its_trigger_by_the_client_that_sent_it(string client, string listed)
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken, extra: static services =>
            _ = services.Replace(ServiceDescriptor.Singleton<IGraphRunStarter>(static provider => new RecordingStarter(provider.GetRequiredService<IWorkflowRunStore>(), provider.GetRequiredService<TimeProvider>()))));
        await Create(host);

        var started = await host.Http.Ask(Post($"/sindri/workflows/{_slug.Value}/runs", "{\"payload\":null}", Human(host.Token.Value, client)));
        var runs = await host.Http.Ask(Get($"/sindri/workflows/{_slug.Value}/runs", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, started.Status);
        using var body = JsonDocument.Parse(runs.Body);
        Assert.Equal(listed, body.RootElement.GetProperty("runs")[0].GetProperty("trigger").GetString());
    }

    private static async Task Create(GoldenHost host)
    {
        var spec = new GraphSpec(Version: 1, Name: "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        var workflows = host.App.Services.GetRequiredService<IWorkflowStore>();
        _ = (await workflows.Create(_id, _slug, spec, "owner", TestContext.Current.CancellationToken)).Value;
        _ = (await workflows.Activate(_id, 1, TestContext.Current.CancellationToken)).Value;
    }

    // Records the run with the trigger the route built (its 'by' is the caller's actor), as GraphRunStarter does before it starts the Temporal workflow.
    private sealed class RecordingStarter(IWorkflowRunStore runs, TimeProvider clock) : IGraphRunStarter
    {
        public async ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
        {
            var id = WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence);
            var run = new WorkflowRun(id, request.Workflow, request.Revision, request.Trigger, WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: null);
            var inserted = await runs.Insert(run, cancellationToken);
            return inserted.IsError ? inserted.Error : new GraphStart(GraphStartKind.Started, id);
        }
    }
}
