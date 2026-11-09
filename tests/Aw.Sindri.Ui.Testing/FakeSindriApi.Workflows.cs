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
    private readonly List<string> _startedWorkflows = [];
    private readonly List<string> _cancelledWorkflowRuns = [];

    /// <summary>POST /sindri/workflows/{slug}/runs's answer: the started body by default (a refusal golden, such as workflows-slug-runs/not-active, to test a refusal).</summary>
    public RouteAnswer StartWorkflowRunAnswer { get; set; } = RouteAnswer.SpecShaped("workflows-slug-runs", "started");

    /// <summary>POST /sindri/workflow-runs/{run}/cancel's answer: the cancelled body by default.</summary>
    public RouteAnswer CancelWorkflowRunAnswer { get; set; } = RouteAnswer.SpecShaped("workflow-runs-run-cancel", "cancelled");

    /// <summary>When set, the next Run now waits for it before it answers.</summary>
    public TaskCompletionSource? StartWorkflowRunGate { get; set; }

    /// <summary>Every workflow a run was started for, in order.</summary>
    public IReadOnlyList<string> StartedWorkflows { get { lock (_gate) { return [.. _startedWorkflows]; } } }

    /// <summary>Every run a cancel was sent for, in order.</summary>
    public IReadOnlyList<string> CancelledWorkflowRuns { get { lock (_gate) { return [.. _cancelledWorkflowRuns]; } } }

    /// <summary>GET /sindri/workflows's answer: the two-workflow golden by default.</summary>
    public RouteAnswer WorkflowsAnswer { get; set; } = RouteAnswer.Api("workflows", "list-two");

    /// <summary>GET /sindri/workflows/{slug}'s answer per slug; a slug not here answers the SND-GRAPH-008 golden.</summary>
    public Dictionary<string, RouteAnswer> WorkflowAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>GET /sindri/workflows/{slug}/runs's answer per slug; a slug not here answers the SND-GRAPH-008 golden.</summary>
    public Dictionary<string, RouteAnswer> WorkflowRunsAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>GET /sindri/workflow-runs/{run}'s answer per run id; an id not here answers the SND-GRAPH-101 golden.</summary>
    public Dictionary<string, RouteAnswer> WorkflowRunAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>When set, the list read waits for it before answering (a test holds the read in flight); unset by default, so the read answers at once.</summary>
    public TaskCompletionSource? WorkflowsGate { get; set; }

    /// <summary>Per slug, a gate the workflow read and the runs read wait for before answering; a slug not here answers at once.</summary>
    public Dictionary<string, TaskCompletionSource> WorkflowGates { get; } = new(StringComparer.Ordinal);

    /// <summary>Per run id, a gate the run read waits for before answering; an id not here answers at once.</summary>
    public Dictionary<string, TaskCompletionSource> WorkflowRunGates { get; } = new(StringComparer.Ordinal);

    /// <summary>How many times the list was read.</summary>
    public int WorkflowsCalls => Volatile.Read(ref _workflowsCalls);

    /// <summary>How many times one workflow was read.</summary>
    public int WorkflowCalls => Volatile.Read(ref _workflowCalls);

    /// <summary>How many times a workflow's runs were read.</summary>
    public int WorkflowRunsCalls => Volatile.Read(ref _workflowRunsCalls);

    /// <summary>How many times one run was read.</summary>
    public int WorkflowRunCalls => Volatile.Read(ref _workflowRunCalls);

    public async Task<Result<WorkflowList>> Workflows(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _workflowsCalls);
        await WaitFor(WorkflowsGate, cancellationToken).ConfigureAwait(false);
        return Answer<WorkflowList>(api, WorkflowRoutes.List(), WorkflowsAnswer);
    }

    public async Task<Result<WorkflowView>> Workflow(Uri api, string slug, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _workflowCalls);
        await WaitFor(WorkflowGates.GetValueOrDefault(slug), cancellationToken).ConfigureAwait(false);
        var answer = WorkflowAnswers.TryGetValue(slug, out var own) ? own : RouteAnswer.Problem("workflows-slug", "not-found");
        return Answer<WorkflowView>(api, WorkflowRoutes.One(slug), answer);
    }

    public async Task<Result<WorkflowRunList>> WorkflowRuns(Uri api, string slug, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _workflowRunsCalls);
        await WaitFor(WorkflowGates.GetValueOrDefault(slug), cancellationToken).ConfigureAwait(false);
        var answer = WorkflowRunsAnswers.TryGetValue(slug, out var own) ? own : RouteAnswer.Problem("workflows-slug-runs", "unknown-workflow");
        return Answer<WorkflowRunList>(api, WorkflowRoutes.Runs(slug), answer);
    }

    public async Task<Result<WorkflowRunView>> WorkflowRun(Uri api, string runId, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _workflowRunCalls);
        await WaitFor(WorkflowRunGates.GetValueOrDefault(runId), cancellationToken).ConfigureAwait(false);
        var answer = WorkflowRunAnswers.TryGetValue(runId, out var own) ? own : RouteAnswer.Problem("workflow-runs-run", "not-found");
        return Answer<WorkflowRunView>(api, WorkflowRoutes.Run(runId), answer);
    }

    public async Task<Result<WorkflowRunStarted>> StartWorkflowRun(Uri api, string slug, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _startedWorkflows.Add(slug);
        }

        await WaitFor(StartWorkflowRunGate, cancellationToken).ConfigureAwait(false);
        return Answer<WorkflowRunStarted>(api, WorkflowRoutes.Runs(slug), StartWorkflowRunAnswer);
    }

    public Task<Result<WorkflowRunCancelled>> CancelWorkflowRun(Uri api, string runId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _cancelledWorkflowRuns.Add(runId);
        }

        return Task.FromResult(Answer<WorkflowRunCancelled>(api, WorkflowRoutes.Cancel(runId), CancelWorkflowRunAnswer));
    }
}
