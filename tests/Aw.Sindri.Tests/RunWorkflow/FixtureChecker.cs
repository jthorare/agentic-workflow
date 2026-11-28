using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Tests.Graph;

namespace Aw.Sindri.Tests.RunWorkflow;

/// <summary>The activation checks over the graph tests' fixture blocks, for a run that names a saved revision (#3691).</summary>
internal sealed class FixtureChecker : IWorkflowChecker
{
    public ValueTask<GraphValidator> Validator(CancellationToken cancellationToken) => ValueTask.FromResult(GraphFixtures.Validator());
}
