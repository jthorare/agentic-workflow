using Aw.Context.Core.Models;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Host.Models;

/// <summary>Answers a prompt block's question through the routed model port under the 'workflowPrompt' route.</summary>
/// <param name="models">The routed model port.</param>
internal sealed class GraphModelPortAdapter(IModelPort models) : IGraphModelPort
{
    private const int MaxTokens = 4096;

    private static readonly TimeSpan _budget = TimeSpan.FromMinutes(5);

    public async ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var answer = await models.Ask(new ModelRequest(ModelPurpose.WorkflowPrompt, prompt, MaxTokens, _budget), cancellationToken).ConfigureAwait(false);
        return answer.IsError ? answer.Error : answer.Value.Text;
    }
}
