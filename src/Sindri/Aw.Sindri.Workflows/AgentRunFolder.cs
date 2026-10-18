using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Makes an agent run's folder and puts the Step's declared inputs in it before the launch (sandbox spec SB17).</summary>
/// <param name="home">The Sindri home.</param>
/// <param name="contents">Reads a fact's content.</param>
internal sealed class AgentRunFolder(WorkflowsHome home, IFactContents contents)
{
    public ValueTask<Result<string>> Prepare(AgentStepRun run, IReadOnlyDictionary<string, FactValue> inputs, CancellationToken cancellationToken)
    {
        _ = (home, contents, run, inputs, cancellationToken);
        throw new NotSupportedException("the run folder is not built yet");
    }
}
