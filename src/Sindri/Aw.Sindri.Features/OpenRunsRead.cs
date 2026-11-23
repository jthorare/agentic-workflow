using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features;

/// <summary>
/// <see cref="IOpenRuns"/> over the live stores (first-run spec decision 11): the launched agent sessions (what scripts/lib/sindri-open-runs.sh counts:
/// a session with a launch), the workflow runs that are running or waiting, and the running jobs, in that order. A superset of the bash guard: a workflow run
/// in a code node or an approval wait has no agent session, and a job has none either. It sits beside the slices because the restart and the
/// GET /sindri/runs/open read both use it.
/// </summary>
/// <param name="snapshot">The collector's last pass.</param>
/// <param name="workflows">The workflows, for each one's name and slug.</param>
/// <param name="runs">The runs in flight.</param>
/// <param name="jobs">The 'jobs' view.</param>
internal sealed class OpenRunsRead(ISessionSnapshot snapshot, IWorkflowStore workflows, IWorkflowRunStore runs, IJobViews jobs) : IOpenRuns
{
    public async ValueTask<Result<IReadOnlyList<OpenRun>>> Read(CancellationToken cancellationToken)
    {
        var open = new List<OpenRun>();
        open.AddRange(Sessions());
        var flowing = await WorkflowRuns(cancellationToken).ConfigureAwait(false);
        if (flowing.IsError)
        {
            return flowing.Error;
        }

        open.AddRange(flowing.Value);
        var running = await jobs.List(JobState.Running, kind: null, cancellationToken).ConfigureAwait(false);
        if (running.IsError)
        {
            return running.Error;
        }

        open.AddRange(running.Value.OrderBy(static j => j.StartedAt).Select(Job));
        return open;
    }

    private IEnumerable<OpenRun> Sessions() =>
        (snapshot.Current?.Sessions ?? []).Where(static s => s.LaunchId is not null).Select(static s =>
            new OpenRun("session", s.Ref.ToString(), $"{(string.IsNullOrWhiteSpace(s.Title) ? s.Ref.Provider.Value : s.Title)} (an agent session)",
                $"sindri://session/{Uri.EscapeDataString(s.Ref.Provider.Value)}/{Uri.EscapeDataString(s.Ref.Id.Value)}"));

    private async ValueTask<Result<IReadOnlyList<OpenRun>>> WorkflowRuns(CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var open = new List<OpenRun>();
        foreach (var workflow in listed.Value)
        {
            var inFlight = await runs.InFlight(workflow.Id, cancellationToken).ConfigureAwait(false);
            if (inFlight.IsError)
            {
                return inFlight.Error;
            }

            open.AddRange(inFlight.Value.Where(static r => r.Status is WorkflowRunStatus.Running or WorkflowRunStatus.Waiting).Select(r => Run(workflow, r)));
        }

        return open;
    }

    private static OpenRun Run(WorkflowRecord workflow, WorkflowRun run) =>
        new("workflow-run", run.Id.Value,
            string.Create(CultureInfo.InvariantCulture, $"{workflow.Name} (run {run.Id.Occurrence}, {(run.Status == WorkflowRunStatus.Waiting ? "waiting" : "running")})"),
            $"sindri://workflow/{workflow.Slug.Value}/runs/{Uri.EscapeDataString(run.Id.Value)}");

    private static OpenRun Job(JobRow job) =>
        new("job", job.Id.Value, job.Repo is { } repo ? $"{job.Kind.Value} of {repo.Value} (a job)" : $"{job.Kind.Value} (a job)", $"sindri://job/{Uri.EscapeDataString(job.Id.Value)}");
}
