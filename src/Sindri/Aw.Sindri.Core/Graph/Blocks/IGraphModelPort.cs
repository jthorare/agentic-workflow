using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The model call of a 'prompt' node. Core cannot reference the Context package, so Host adapts this onto Context's IModelPort,
/// asking with purpose WorkflowPrompt (route 'workflowPrompt') and marking connection data in the prompt as untrusted.
/// </summary>
public interface IGraphModelPort
{
    /// <summary>Asks the model.</summary>
    /// <param name="prompt">The interpolated prompt.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer text, or why the model could not answer.</returns>
    public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken);
}
