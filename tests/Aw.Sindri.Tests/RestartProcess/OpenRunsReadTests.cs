using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.RestartProcess;

/// <summary>
/// The open set (first-run spec decision 11, A4): a superset of scripts/lib/sindri-open-runs.sh. Its bash cases (a session with a launch counts, one
/// without does not), plus a workflow run with no agent session (running or waiting) and a running job; queued and ended runs and ended jobs are not open.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class OpenRunsReadTests
{
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly InMemorySessionSnapshot _snapshot = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly InMemoryJobViews _jobs = new();

    public OpenRunsReadTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private OpenRunsRead Read => new(_snapshot, _workflows, _runs, _jobs);

    [Fact]
    public async Task Nothing_open_is_an_empty_set()
    {
        var open = await Read.Read(Token);

        Assert.True(open.IsSuccess);
        Assert.Empty(open.Value);
    }

    [Fact]
    public async Task A_launched_session_is_open_and_one_the_owner_started_is_not()
    {
        _snapshot.Publish(new SessionSnapshot([Session("aaaaaaaa-0000-4000-8000-000000000001", launched: true), Session("aaaaaaaa-0000-4000-8000-000000000002", launched: false)], [], [], KnownInstants.Epoch));

        var open = (await Read.Read(Token)).Value;

        var only = Assert.Single(open);
        Assert.Equal(("session", "claude-code/aaaaaaaa-0000-4000-8000-000000000001", "Fix login (an agent session)"), (only.Kind, only.Ref, only.Name));
        Assert.Equal("sindri://session/claude-code/aaaaaaaa-0000-4000-8000-000000000001", only.Link);
    }

    [Fact]
    public async Task Running_and_waiting_workflow_runs_are_open_and_queued_or_ended_ones_are_not()
    {
        var workflow = await Workflow("nightly-gate", "Nightly gate");
        await Run(workflow, "nightly-gate", 14, WorkflowRunStatus.Running);
        await Run(workflow, "nightly-gate", 15, WorkflowRunStatus.Waiting);
        await Run(workflow, "nightly-gate", 16, WorkflowRunStatus.Queued);
        await Run(workflow, "nightly-gate", 13, WorkflowRunStatus.Succeeded);

        var open = (await Read.Read(Token)).Value;

        Assert.Equal(["Nightly gate (run 14, running)", "Nightly gate (run 15, waiting)"], [.. open.Select(static r => r.Name)], StringComparer.Ordinal);
        Assert.All(open, static r => Assert.Equal("workflow-run", r.Kind));
        Assert.Equal("sindri://workflow/nightly-gate/runs/nightly-gate%401%3A14", open[0].Link);
    }

    [Fact]
    public async Task A_running_job_is_open_and_an_ended_one_is_not()
    {
        _jobs.Put(A.Job().WithId(JobId.From("JOB-00000000000000000000000000000001")).OfKind(JobKind.From("BuildCodeIndex"), JobKey.From("agentic-workflow")).InRepo(RepoName.From("agentic-workflow")).Build());
        _jobs.Put(A.Job().WithId(JobId.From("JOB-00000000000000000000000000000002")).Ended(JobState.Succeeded).Build());

        var open = (await Read.Read(Token)).Value;

        var only = Assert.Single(open);
        Assert.Equal(("job", "BuildCodeIndex of agentic-workflow (a job)", "sindri://job/JOB-00000000000000000000000000000001"), (only.Kind, only.Name, only.Link));
    }

    [Fact]
    public async Task The_set_lists_sessions_then_workflow_runs_then_jobs()
    {
        _snapshot.Publish(new SessionSnapshot([Session("aaaaaaaa-0000-4000-8000-000000000001", launched: true)], [], [], KnownInstants.Epoch));
        await Run(await Workflow("weekday-check", "Weekday check"), "weekday-check", 7, WorkflowRunStatus.Running);
        _jobs.Put(A.Job().Build());

        var open = (await Read.Read(Token)).Value;

        Assert.Equal(["session", "workflow-run", "job"], [.. open.Select(static r => r.Kind)], StringComparer.Ordinal);
    }

    private static ObservedSession Session(string id, bool launched) =>
        new(new SessionRef(AgentId.From("claude-code"), SessionId.From(id)), 1, 1, SessionState.Running, StateSource.Record, KnownInstants.Epoch)
        {
            LaunchId = launched ? A.FirstLaunchId : null,
            Title = "Fix login",
        };

    private async Task<WorkflowId> Workflow(string slug, string name)
    {
        var id = WorkflowId.From("WF-" + slug.Length.ToString("D32", CultureInfo.InvariantCulture));
        var spec = new GraphSpec(Version: 1, name, Description: null, Concurrency: null, Resources: null, Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(id, WorkflowSlug.From(slug), spec, "owner", Token)).Value;
        return id;
    }

    private async Task Run(WorkflowId workflow, string slug, int occurrence, WorkflowRunStatus status)
    {
        var id = WorkflowRunId.From(string.Create(CultureInfo.InvariantCulture, $"{slug}@1:{occurrence}"));
        var ended = status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled;
        _ = (await _runs.Insert(new WorkflowRun(id, workflow, 1, """{"kind":"manual"}""", status, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch.AddMinutes(occurrence),
            ended ? KnownInstants.Epoch.AddMinutes(occurrence + 1) : null), Token)).Value;
    }
}
