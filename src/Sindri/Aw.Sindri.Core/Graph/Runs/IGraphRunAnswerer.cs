using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Hands an answer to the 'ask_owner' node a run's workflow waits at (a port so the answer slice is testable without a Temporal server).</summary>
public interface IGraphRunAnswerer
{
    /// <summary>Delivers the answer as a Temporal Update; the workflow checks it and takes it, or refuses.</summary>
    /// <param name="answer">The answer.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>Success, or why the workflow did not take the answer.</returns>
    public ValueTask<Result<Unit>> Answer(GraphAnswer answer, CancellationToken cancellationToken);
}
