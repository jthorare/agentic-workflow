using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'ask_owner' block: posts a question as a Needs-you row; the workflow waits for an answer (or the timeout) and the node leaves by 'answered' or 'timeout'.</summary>
/// <param name="questions">Where the question is written.</param>
/// <param name="clock">The clock; the system clock when null.</param>
public sealed class AskOwnerBlock(IQuestionPort questions, TimeProvider? clock = null) : IGraphBlock
{
    /// <inheritdoc />
    public string Type => "ask_owner";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([]);

    /// <inheritdoc />
    public string Example => "{}";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => throw new NotSupportedException();

    /// <summary>How long the node waits for an answer.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The wait, or null when the node waits for ever.</returns>
    public static TimeSpan? TimeoutOf(JsonObject config) => throw new NotSupportedException();

    /// <summary>The answers the node offers.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The choices; empty when any text is an answer.</returns>
    public static IReadOnlyList<string> ChoicesOf(JsonObject config) => throw new NotSupportedException();

    /// <summary>Checks an answer against the choices.</summary>
    /// <param name="choices">The choices the node offers.</param>
    /// <param name="text">A free-text answer, or null.</param>
    /// <param name="choice">A picked choice, or null.</param>
    /// <returns>What is wrong with the answer, or null when it is acceptable.</returns>
    public static string? CheckAnswer(IReadOnlyList<string> choices, string? text, string? choice) => throw new NotSupportedException();

    /// <summary>What the node hands on once it is answered or has timed out.</summary>
    /// <param name="config">The node's config.</param>
    /// <param name="answer">The answer, or null when the timeout passed with none.</param>
    /// <param name="answeredBy">Who answered ('owner', 'session:&lt;id&gt;'); ignored when <paramref name="answer"/> is null.</param>
    /// <returns>The outputs and the branch taken.</returns>
    public static AskOwnerOutcome Resolve(JsonObject config, string? answer, string? answeredBy) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) => throw new NotSupportedException($"{questions}{clock}");
}
