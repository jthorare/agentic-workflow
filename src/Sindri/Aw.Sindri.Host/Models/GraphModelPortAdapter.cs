using Aw.Context.Core.Models;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Host.Models;

// why: registered in the fix commit; until then only tests build it.
#pragma warning disable MA0182
/// <summary>Answers a prompt block's question through the routed model port under the 'workflowPrompt' route.</summary>
/// <param name="models">The routed model port.</param>
internal sealed class GraphModelPortAdapter(IModelPort models) : IGraphModelPort
{
    public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
    {
        _ = models;
        throw new NotSupportedException();
    }
}
#pragma warning restore MA0182
