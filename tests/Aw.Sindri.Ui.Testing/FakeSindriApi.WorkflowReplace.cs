using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The fake's PUT /sindri/workflows/{slug} (<see cref="FakeSindriApi"/>, continued): what Undo sends.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<ReplacedWorkflow> _replaced = [];

    /// <summary>One PUT the fake got.</summary>
    /// <param name="Slug">The workflow.</param>
    /// <param name="BaseRevision">The base revision sent.</param>
    /// <param name="Spec">The graph, as the JSON text sent.</param>
    internal sealed record ReplacedWorkflow(string Slug, int BaseRevision, string Spec);

    /// <summary>PUT /sindri/workflows/{slug}'s answer: the replaced golden by default.</summary>
    public RouteAnswer ReplaceWorkflowAnswer { get; set; } = RouteAnswer.Api("workflows-slug", "replaced");

    /// <summary>The PUTs sent, in order.</summary>
    internal IReadOnlyList<ReplacedWorkflow> ReplacedWorkflows { get { lock (_gate) { return [.. _replaced]; } } }

    public Task<Result<WorkflowOpsApplied>> ReplaceWorkflow(Uri api, string slug, int baseRevision, string specJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _replaced.Add(new ReplacedWorkflow(slug, baseRevision, specJson));
        }

        return Task.FromResult(Answer<WorkflowOpsApplied>(api, WorkflowRoutes.One(slug), ReplaceWorkflowAnswer));
    }
}
