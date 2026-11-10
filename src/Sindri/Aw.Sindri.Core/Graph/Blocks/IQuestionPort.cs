using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where an 'ask_owner' node's question goes: a Needs-you row that waits for an answer.</summary>
public interface IQuestionPort
{
    /// <summary>Writes the question.</summary>
    /// <param name="ask">The question, with its choices, default and deadline.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the question could not be written.</returns>
    public ValueTask<Result<Unit>> Post(QuestionAsk ask, CancellationToken cancellationToken);
}
