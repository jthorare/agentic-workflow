using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>E1 (Home spec 2026-11-12 §3.2): a live run that ended failed and is unseen is a 'run-failed' needs-you row, read from the run store at request time.</summary>
[Lane(TestLane.Unit)]
public sealed class RunFailedRowsTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("nightly");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WorkflowRun Run(int occurrence, WorkflowRunStatus status, string mode = WorkflowRunMode.Live, DateTimeOffset? seen = null) =>
        new(WorkflowRunId.Of(_slug, 3, occurrence), _workflow, 3, """{"kind":"manual"}""", status, Outputs: null, Error: "node 'agent' failed", ItemId: null, KnownInstants.Epoch, KnownInstants.Epoch.AddMinutes(2), mode, TestOptions: null, seen);

    [Fact]
    public void A_failed_run_is_a_run_failed_row_naming_the_run_and_why_it_failed()
    {
        var row = NeedsYouRows.ForRunFailed(Run(12, WorkflowRunStatus.Failed), new PassThroughScrubber()).Value;

        Assert.Equal("run-failed:nightly@3:12", row.Id.Value);
        Assert.Equal(NeedsYouKind.RunFailed, row.Kind);
        Assert.Equal(new NeedsYouSubject(SubjectKind.WorkflowRun, SubjectId.From("nightly.3:12")), row.Subject);
        Assert.Contains("nightly", row.Title, StringComparison.Ordinal);
        Assert.Equal("node 'agent' failed", row.Reason);
        Assert.Equal(Urgency.Colour, row.Urgency);
        Assert.Equal(KnownInstants.Epoch.AddMinutes(2), row.Since);
        Assert.Equal(NeedsYouRoutes.WorkflowRun(WorkflowRunId.From("nightly@3:12")), row.Open.Route);
    }

    [Fact]
    public async Task Only_failed_live_unseen_runs_make_rows()
    {
        var runs = new InMemoryWorkflowRunStore(new FakeTimeProvider(KnownInstants.Epoch));
        foreach (var run in new[]
        {
            Run(1, WorkflowRunStatus.Failed), Run(2, WorkflowRunStatus.Failed, seen: KnownInstants.Epoch), Run(3, WorkflowRunStatus.Succeeded),
            Run(4, WorkflowRunStatus.Cancelled), Run(500, WorkflowRunStatus.Failed, WorkflowRunMode.Test),
        })
        {
            _ = (await runs.Insert(run, Token)).Value;
        }

        var rows = await NeedsYouLiveRows.FailedRunRows(runs, new PassThroughScrubber(), Token);

        Assert.Equal(["run-failed:nightly@3:1"], rows.Select(static r => r.Id.Value), StringComparer.Ordinal);
    }
}
