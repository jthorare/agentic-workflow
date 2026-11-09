using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The model call of a 'prompt' node. Core cannot reference the Context package, so Host adapts this onto Context's IModelPort,
/// asking with purpose WorkflowPrompt (route 'workflowPrompt'). The prompt block fences every trigger and node-output reference through <see cref="Untrusted"/>; Host does not scan the prompt.
/// </summary>
public interface IGraphModelPort
{
    /// <summary>Asks the model.</summary>
    /// <param name="prompt">The interpolated prompt.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer text, or why the model could not answer.</returns>
    public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken);

    /// <summary>Wraps connection text in the untrusted fence the model reads as data.</summary>
    /// <param name="source">Where the text came from: "workflow:trigger" or "workflow:node:&lt;nodeId&gt;".</param>
    /// <param name="text">The connection text.</param>
    /// <returns>The fenced text.</returns>
    public string Untrusted(string source, string text);
}
