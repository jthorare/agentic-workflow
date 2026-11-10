using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;
namespace Aw.Sindri.Tests.Workflows;

/// <summary>An <see cref="IQuestionPort"/> for the walk tests: remembers each question an 'ask_owner' node posted.</summary>
internal sealed class GraphTestQuestions : IQuestionPort
{
    private readonly ConcurrentQueue<string> _asked = new();

    /// <summary>'&lt;run&gt;/&lt;node&gt;' of each question, in order.</summary>
    public IReadOnlyList<string> Asked => [.. _asked];

    public ValueTask<Result<Unit>> Post(QuestionAsk ask, CancellationToken cancellationToken)
    {
        _asked.Enqueue($"{ask.Run.Value}/{ask.Node.Value}");
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
