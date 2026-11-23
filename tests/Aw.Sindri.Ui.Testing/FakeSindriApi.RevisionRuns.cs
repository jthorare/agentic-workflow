using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The fake's run of a saved revision once (#3691): POST /sindri/workflows/{slug}/runs with a revision. It answers <see cref="StartWorkflowRunAnswer"/>, the same route's answer, and waits on <see cref="StartWorkflowRunGate"/>.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<(string Slug, int Revision)> _startedRevisionRuns = [];

    /// <summary>Every workflow and revision a run-once was started for, in order.</summary>
    public IReadOnlyList<(string Slug, int Revision)> StartedRevisionRuns { get { lock (_gate) { return [.. _startedRevisionRuns]; } } }

    public async Task<Result<WorkflowRunStarted>> StartWorkflowRevisionRun(Uri api, string slug, int revision, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _startedRevisionRuns.Add((slug, revision));
        }

        await WaitFor(StartWorkflowRunGate, cancellationToken).ConfigureAwait(false);
        return Answer<WorkflowRunStarted>(api, WorkflowRoutes.Runs(slug), StartWorkflowRunAnswer);
    }
}
