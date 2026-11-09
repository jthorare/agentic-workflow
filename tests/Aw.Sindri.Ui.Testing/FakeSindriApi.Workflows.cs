using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The workflow reads of the fake (#2258 PR 8a): each answers the API's goldens, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private int _workflowsCalls;
    private int _workflowCalls;
    private int _workflowRunsCalls;
    private int _workflowRunCalls;

    /// <summary>GET /sindri/workflows's answer: the two-workflow golden by default.</summary>
    public RouteAnswer WorkflowsAnswer { get; set; } = RouteAnswer.Api("workflows", "list-two");

    /// <summary>GET /sindri/workflows/{slug}'s answer per slug; a slug not here answers the SND-GRAPH-008 golden.</summary>
    public Dictionary<string, RouteAnswer> WorkflowAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>GET /sindri/workflows/{slug}/runs's answer per slug; a slug not here answers the SND-GRAPH-008 golden.</summary>
    public Dictionary<string, RouteAnswer> WorkflowRunsAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>GET /sindri/workflow-runs/{run}'s answer per run id; an id not here answers the SND-GRAPH-101 golden.</summary>
    public Dictionary<string, RouteAnswer> WorkflowRunAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>How many times the list was read.</summary>
    public int WorkflowsCalls => Volatile.Read(ref _workflowsCalls);

    /// <summary>How many times one workflow was read.</summary>
    public int WorkflowCalls => Volatile.Read(ref _workflowCalls);

    /// <summary>How many times a workflow's runs were read.</summary>
    public int WorkflowRunsCalls => Volatile.Read(ref _workflowRunsCalls);

    /// <summary>How many times one run was read.</summary>
    public int WorkflowRunCalls => Volatile.Read(ref _workflowRunCalls);

    public Task<Result<WorkflowList>> Workflows(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _workflowsCalls);
        return Task.FromResult(Answer<WorkflowList>(api, WorkflowRoutes.List(), WorkflowsAnswer));
    }

    public Task<Result<WorkflowView>> Workflow(Uri api, string slug, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _workflowCalls);
        var answer = WorkflowAnswers.TryGetValue(slug, out var own) ? own : RouteAnswer.Problem("workflows-slug", "not-found");
        return Task.FromResult(Answer<WorkflowView>(api, WorkflowRoutes.One(slug), answer));
    }

    public Task<Result<WorkflowRunList>> WorkflowRuns(Uri api, string slug, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _workflowRunsCalls);
        var answer = WorkflowRunsAnswers.TryGetValue(slug, out var own) ? own : RouteAnswer.Problem("workflows-slug-runs", "unknown-workflow");
        return Task.FromResult(Answer<WorkflowRunList>(api, WorkflowRoutes.Runs(slug), answer));
    }

    public Task<Result<WorkflowRunView>> WorkflowRun(Uri api, string runId, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _workflowRunCalls);
        var answer = WorkflowRunAnswers.TryGetValue(runId, out var own) ? own : RouteAnswer.Problem("workflow-runs-run", "not-found");
        return Task.FromResult(Answer<WorkflowRunView>(api, WorkflowRoutes.Run(runId), answer));
    }
}
