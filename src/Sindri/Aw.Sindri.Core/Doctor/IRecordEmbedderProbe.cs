using Aw.Kernel;

namespace Aw.Sindri.Core.Doctor;

/// <summary>Asks the records embedder's server (Ollama on 127.0.0.1:11434) whether it answers and holds the pinned model; doctor's 'context:index-ollama' row.</summary>
public interface IRecordEmbedderProbe
{
    /// <summary>The model the records embedder uses, for the row's text.</summary>
    public string Model { get; }

    /// <summary>One bounded look at the server. It never pulls a model.</summary>
    /// <param name="cancellationToken">Cancels the look.</param>
    /// <returns>Unit when the server answers with the model; otherwise the reason, which names no value the server held.</returns>
    public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken);
}
