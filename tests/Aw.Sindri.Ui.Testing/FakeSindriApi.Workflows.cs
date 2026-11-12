using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The workflow reads of the fake (#2258 PR 8a): each answers the API's goldens, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private int _workflowsCalls;
    private int _allWorkflowsCalls;
    private int _workflowCalls;
    private int _workflowRunsCalls;
    private int _workflowRunCalls;
    private readonly List<string> _startedWorkflows = [];
    private readonly List<string> _cancelledWorkflowRuns = [];
    private readonly List<string> _agentNodeControls = [];

    private readonly List<string> _activatedWorkflows = [];
    private readonly List<string> _pausedWorkflows = [];
    private readonly List<AppliedOps> _appliedOps = [];
    private readonly List<(string Slug, string Template)> _createdWorkflows = [];

    /// <summary>POST /sindri/workflows/{slug}/runs's answer: the started body by default (a refusal golden, such as workflows-slug-runs/not-active, to test a refusal).</summary>
    public RouteAnswer StartWorkflowRunAnswer { get; set; } = RouteAnswer.SpecShaped("workflows-slug-runs", "started");

    /// <summary>POST /sindri/workflow-runs/{run}/cancel's answer: the cancelled body by default.</summary>
    public RouteAnswer CancelWorkflowRunAnswer { get; set; } = RouteAnswer.SpecShaped("workflow-runs-run-cancel", "cancelled");

    /// <summary>POST .../takeover's answer: taken_over by default.</summary>
    public RouteAnswer TakeOverAgentNodeAnswer { get; set; } = RouteAnswer.Api("workflow-runs-run-nodes-node-takeover", "taken-over");

    /// <summary>POST .../handback's answer: waiting by default.</summary>
    public RouteAnswer HandBackAgentNodeAnswer { get; set; } = RouteAnswer.Api("workflow-runs-run-nodes-node-handback", "handed-back");

    /// <summary>When set, a take over or hand back waits for it before it answers.</summary>
    public TaskCompletionSource? AgentNodeControlGate { get; set; }

    /// <summary>Every take over and hand back sent, in order, as "takeover|handback &lt;run&gt; &lt;node&gt; &lt;item&gt;".</summary>
    public IReadOnlyList<string> AgentNodeControls { get { lock (_gate) { return [.. _agentNodeControls]; } } }

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

    /// <summary>GET /sindri/workflows?includeArchived=true's answer; null serves <see cref="WorkflowsAnswer"/> (no archived rows).</summary>
    public RouteAnswer? AllWorkflowsAnswer { get; set; }

    /// <summary>How many times the includeArchived list was read.</summary>
    public int AllWorkflowsCalls => Volatile.Read(ref _allWorkflowsCalls);

    public Task<Result<WorkflowList>> WorkflowsIncludingArchived(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _allWorkflowsCalls);
        return Task.FromResult(Answer<WorkflowList>(api, WorkflowRoutes.ListAll(), AllWorkflowsAnswer ?? WorkflowsAnswer));
    }

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

    public async Task<Result<AgentNodeControlled>> TakeOverAgentNode(Uri api, string runId, string node, int itemIndex, CancellationToken cancellationToken)
    {
        Record("takeover", runId, node, itemIndex);
        await WaitFor(AgentNodeControlGate, cancellationToken).ConfigureAwait(false);
        return Answer<AgentNodeControlled>(api, WorkflowRoutes.TakeOver(runId, node), TakeOverAgentNodeAnswer);
    }

    public async Task<Result<AgentNodeControlled>> HandBackAgentNode(Uri api, string runId, string node, int itemIndex, CancellationToken cancellationToken)
    {
        Record("handback", runId, node, itemIndex);
        await WaitFor(AgentNodeControlGate, cancellationToken).ConfigureAwait(false);
        return Answer<AgentNodeControlled>(api, WorkflowRoutes.HandBack(runId, node), HandBackAgentNodeAnswer);
    }

    private void Record(string verb, string runId, string node, int itemIndex)
    {
        lock (_gate)
        {
            _agentNodeControls.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{verb} {runId} {node} {itemIndex}"));
        }
    }

    /// <summary>One POST .../ops the fake got.</summary>
    /// <param name="Slug">The workflow.</param>
    /// <param name="BaseRevision">The base revision sent.</param>
    /// <param name="Ops">The ops, as the JSON array text sent.</param>
    internal sealed record AppliedOps(string Slug, int BaseRevision, string Ops);

    /// <summary>GET /sindri/workflows/blocks's answer: a refusal until a test serves a block list.</summary>
    public RouteAnswer WorkflowBlocksAnswer { get; set; } = RouteAnswer.Problem(System.Net.HttpStatusCode.NotFound, "SND-GRAPH-008", "no blocks served");

    /// <summary>POST /sindri/workflows's answer: the created golden by default (a ProblemDetails, such as SND-GRAPH-009 for a taken slug, to test a refusal).</summary>
    public RouteAnswer CreateWorkflowAnswer { get; set; } = RouteAnswer.Api("workflows", "created");

    /// <summary>Holds POST /sindri/workflows until a test releases it (the call is recorded first).</summary>
    public TaskCompletionSource? CreateWorkflowGate { get; set; }

    /// <summary>The slug and template of every create sent, in order.</summary>
    public IReadOnlyList<(string Slug, string Template)> CreatedWorkflows { get { lock (_gate) { return [.. _createdWorkflows]; } } }

    /// <summary>POST .../ops's answer.</summary>
    public RouteAnswer ApplyWorkflowOpsAnswer { get; set; } = RouteAnswer.Api("workflows-slug-ops", "ops-applied");

    /// <summary>Holds POST .../ops until a test releases it (the call is recorded first).</summary>
    public TaskCompletionSource? ApplyWorkflowOpsGate { get; set; }

    /// <summary>POST .../activate's answer.</summary>
    public RouteAnswer ActivateWorkflowAnswer { get; set; } = RouteAnswer.Api("workflows-slug-activate", "activated");

    /// <summary>POST .../pause's answer.</summary>
    public RouteAnswer PauseWorkflowAnswer { get; set; } = RouteAnswer.Api("workflows-slug-pause", "paused");

    /// <summary>Holds POST .../activate and .../pause until a test releases it (the call is recorded first).</summary>
    public TaskCompletionSource? WorkflowStateGate { get; set; }

    /// <summary>The workflows Activate was sent for, in order.</summary>
    public IReadOnlyList<string> ActivatedWorkflows { get { lock (_gate) { return [.. _activatedWorkflows]; } } }

    /// <summary>The workflows Pause was sent for, in order.</summary>
    public IReadOnlyList<string> PausedWorkflows { get { lock (_gate) { return [.. _pausedWorkflows]; } } }

    /// <summary>The ops sent, in order.</summary>
    internal IReadOnlyList<AppliedOps> AppliedWorkflowOps { get { lock (_gate) { return [.. _appliedOps]; } } }

    public Task<Result<WorkflowBlockList>> WorkflowBlocks(Uri api, CancellationToken cancellationToken) =>
        Task.FromResult(Answer<WorkflowBlockList>(api, WorkflowRoutes.Blocks(), WorkflowBlocksAnswer));

    public async Task<Result<WorkflowCreated>> CreateWorkflow(Uri api, string slug, string template, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _createdWorkflows.Add((slug, template));
        }

        await WaitFor(CreateWorkflowGate, cancellationToken).ConfigureAwait(false);
        return Answer<WorkflowCreated>(api, WorkflowRoutes.Create(), CreateWorkflowAnswer);
    }

    public async Task<Result<WorkflowOpsApplied>> ApplyWorkflowOps(Uri api, string slug, int baseRevision, string opsJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _appliedOps.Add(new AppliedOps(slug, baseRevision, opsJson));
        }

        await WaitFor(ApplyWorkflowOpsGate, cancellationToken).ConfigureAwait(false);
        return Answer<WorkflowOpsApplied>(api, WorkflowRoutes.Ops(slug), ApplyWorkflowOpsAnswer);
    }

    public async Task<Result<WorkflowActivated>> ActivateWorkflow(Uri api, string slug, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _activatedWorkflows.Add(slug);
        }

        await WaitFor(WorkflowStateGate, cancellationToken).ConfigureAwait(false);
        return Answer<WorkflowActivated>(api, WorkflowRoutes.Activate(slug), ActivateWorkflowAnswer);
    }

    public async Task<Result<WorkflowPaused>> PauseWorkflow(Uri api, string slug, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _pausedWorkflows.Add(slug);
        }

        await WaitFor(WorkflowStateGate, cancellationToken).ConfigureAwait(false);
        return Answer<WorkflowPaused>(api, WorkflowRoutes.Pause(slug), PauseWorkflowAnswer);
    }

    // The routes of the Workflows page's second round (#3192). Until the engine builds them they answer SND-GRAPH-113, which the page shows as "not built yet", never as an error.
    private readonly List<(string Slug, int? Revision)> _validated = [];
    private readonly List<(string Slug, TestWorkflowBody Body)> _tested = [];
    private readonly List<(string Slug, PinWorkflowBody Body)> _pinnedOutputs = [];
    private int _templatesCalls;

    /// <summary>The answer of a route the engine has not built yet.</summary>
    public static RouteAnswer NotBuilt { get; } = RouteAnswer.Problem(System.Net.HttpStatusCode.NotImplemented, "SND-GRAPH-113", "This is not built yet.");

    /// <summary>POST /sindri/workflows/validate's answer.</summary>
    public RouteAnswer ValidateWorkflowAnswer { get; set; } = NotBuilt;

    /// <summary>POST .../test's answer.</summary>
    public RouteAnswer TestWorkflowAnswer { get; set; } = NotBuilt;

    /// <summary>POST .../pins's answer.</summary>
    public RouteAnswer PinWorkflowAnswer { get; set; } = NotBuilt;

    /// <summary>GET /sindri/workflows/templates's answer.</summary>
    public RouteAnswer WorkflowTemplatesAnswer { get; set; } = NotBuilt;

    /// <summary>GET /sindri/workflows/trigger-events's answer.</summary>
    public RouteAnswer TriggerEventsAnswer { get; set; } = NotBuilt;

    /// <summary>Holds GET /sindri/workflows/templates until a test releases it (the call is counted first).</summary>
    public TaskCompletionSource? WorkflowTemplatesGate { get; set; }

    /// <summary>How many times the templates were read.</summary>
    public int WorkflowTemplatesCalls => Volatile.Read(ref _templatesCalls);

    /// <summary>The slug and revision of every validate sent, in order.</summary>
    public IReadOnlyList<(string Slug, int? Revision)> ValidatedWorkflows { get { lock (_gate) { return [.. _validated]; } } }

    /// <summary>The slug and body of every test sent, in order.</summary>
    internal IReadOnlyList<(string Slug, TestWorkflowBody Body)> TestedWorkflows { get { lock (_gate) { return [.. _tested]; } } }

    /// <summary>The slug and body of every pin sent, in order.</summary>
    internal IReadOnlyList<(string Slug, PinWorkflowBody Body)> PinnedWorkflows { get { lock (_gate) { return [.. _pinnedOutputs]; } } }

    public Task<Result<WorkflowValidated>> ValidateWorkflow(Uri api, string slug, int? revision, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _validated.Add((slug, revision));
        }

        return Task.FromResult(Answer<WorkflowValidated>(api, WorkflowRoutes.Validate(), ValidateWorkflowAnswer));
    }

    public Task<Result<WorkflowTestStarted>> TestWorkflow(Uri api, string slug, TestWorkflowBody body, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _tested.Add((slug, body));
        }

        return Task.FromResult(Answer<WorkflowTestStarted>(api, WorkflowRoutes.Test(slug), TestWorkflowAnswer));
    }

    public Task<Result<WorkflowPinned>> PinWorkflowOutput(Uri api, string slug, PinWorkflowBody body, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _pinnedOutputs.Add((slug, body));
        }

        return Task.FromResult(Answer<WorkflowPinned>(api, WorkflowRoutes.Pins(slug), PinWorkflowAnswer));
    }

    public async Task<Result<WorkflowTemplateList>> WorkflowTemplates(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _templatesCalls);
        await WaitFor(WorkflowTemplatesGate, cancellationToken).ConfigureAwait(false);
        return Answer<WorkflowTemplateList>(api, WorkflowRoutes.Templates(), WorkflowTemplatesAnswer);
    }

    public Task<Result<TriggerEventList>> TriggerEvents(Uri api, string kind, string? connection, CancellationToken cancellationToken) =>
        Task.FromResult(Answer<TriggerEventList>(api, WorkflowRoutes.TriggerEvents(kind, connection), TriggerEventsAnswer));
}
