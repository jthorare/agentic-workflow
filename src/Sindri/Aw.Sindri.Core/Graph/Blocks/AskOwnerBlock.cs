using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'ask_owner' block: posts a question as a Needs-you row. The node leaves by 'answered' when someone answers ('answeredBy' is the caller's actor: 'human:&lt;client&gt;', 'session:launch:&lt;launch id&gt;' or 'session:&lt;client&gt;') and by 'timeout'
/// when its 'timeout' passes first, handing on the 'default' (empty text when there is none) as 'answer' and 'timeout' as 'answeredBy'. Without a 'timeout' it waits for an answer for ever, until its run is cancelled.
/// The workflow does the waiting; this block writes the question and holds the rules the workflow applies.
/// </summary>
/// <param name="questions">Where the question is written.</param>
/// <param name="clock">The clock.</param>
public sealed class AskOwnerBlock(IQuestionPort questions, TimeProvider clock) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(
        new Dictionary<string, string>(StringComparer.Ordinal) { ["answer"] = "string", ["answeredBy"] = "string" },
        ["answered", "timeout"]);

    /// <inheritdoc />
    public string Type => "ask_owner";

    /// <inheritdoc />
    public string Title => "Ask owner";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Outputs;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("question", BlockFieldType.Text),
        new("choices", BlockFieldType.TextList, Required: false),
        new("timeout", BlockFieldType.Text, Required: false),
        new("default", BlockFieldType.Text, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"question":"Ship the release?","choices":["yes","no"],"timeout":"2h","default":"no"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var problems = new List<(string Field, string Problem)>();
        if (Text(config["timeout"]) is { } timeout && !IsReference(timeout) && BlockDuration.Parse(timeout) is { IsError: true } bad)
        {
            problems.Add(("timeout", bad.Error.Message));
        }

        var choices = ChoicesOf(config);
        if (Text(config["default"]) is { } fallback && !IsReference(fallback) && choices.Count > 0 && !choices.Contains(fallback, StringComparer.Ordinal))
        {
            problems.Add(("default", $"must be one of the choices ({string.Join(", ", choices)}); change it"));
        }

        return problems;
    }

    /// <summary>How long the node waits for an answer.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The wait, or null when the node waits for ever.</returns>
    public static TimeSpan? TimeoutOf(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Text(config["timeout"]) is { } text && BlockDuration.Parse(text) is { IsSuccess: true } parsed ? parsed.Value : null;
    }

    /// <summary>The answers the node offers.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The choices; empty when any text is an answer.</returns>
    public static IReadOnlyList<string> ChoicesOf(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config["choices"] is JsonArray list ? [.. list.Select(Text).OfType<string>()] : [];
    }

    /// <summary>Checks an answer against the choices.</summary>
    /// <param name="choices">The choices the node offers.</param>
    /// <param name="text">A free-text answer, or null.</param>
    /// <param name="choice">A picked choice, or null.</param>
    /// <returns>What is wrong with the answer, or null when it is acceptable.</returns>
    public static string? CheckAnswer(IReadOnlyList<string> choices, string? text, string? choice)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var hasText = !string.IsNullOrWhiteSpace(text);
        var hasChoice = !string.IsNullOrWhiteSpace(choice);
        if (hasText == hasChoice)
        {
            return hasText ? "an answer is only one of a text and a choice" : "the question needs an answer: a text or a choice";
        }

        if (hasChoice && choices.Count == 0)
        {
            return "this question has no choices; answer with a text";
        }

        var given = (hasChoice ? choice : text) ?? string.Empty;
        return choices.Count > 0 && !choices.Contains(given, StringComparer.Ordinal)
            ? $"'{given}' is not one of the choices ({string.Join(", ", choices)})"
            : null;
    }

    /// <summary>What the node hands on once it is answered or has timed out.</summary>
    /// <param name="config">The node's config.</param>
    /// <param name="answer">The answer, or null when the timeout passed with none.</param>
    /// <param name="answeredBy">Who answered (the caller's actor: 'human:&lt;client&gt;', 'session:launch:&lt;launch id&gt;' or 'session:&lt;client&gt;'); ignored when <paramref name="answer"/> is null (the timeout branch).</param>
    /// <returns>The outputs and the branch taken.</returns>
    public static AskOwnerOutcome Resolve(JsonObject config, string? answer, string answeredBy)
    {
        ArgumentNullException.ThrowIfNull(config);
        return answer is null
            ? new AskOwnerOutcome(new JsonObject { ["answer"] = Text(config["default"]) ?? string.Empty, ["answeredBy"] = "timeout" }, "timeout")
            : new AskOwnerOutcome(new JsonObject { ["answer"] = answer, ["answeredBy"] = answeredBy }, "answered");
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var question = Text(context.Config["question"]);
        if (string.IsNullOrWhiteSpace(question))
        {
            return SindriErrors.GraphBlockFailed.With("ask_owner needs a non-empty 'question'");
        }

        if (Text(context.Config["timeout"]) is { } timeoutText && BlockDuration.Parse(timeoutText) is { IsError: true } bad)
        {
            return bad.Error;
        }

        // why: a 'default' written as a reference had no value when the config was checked
        var choices = ChoicesOf(context.Config);
        if (Text(context.Config["default"]) is { } fallback && choices.Count > 0 && !choices.Contains(fallback, StringComparer.Ordinal))
        {
            return SindriErrors.GraphBlockFailed.With($"'default' is '{fallback}', which is not one of the choices ({string.Join(", ", choices)}); change it");
        }

        var timeoutAt = TimeoutOf(context.Config) is { } wait ? clock.GetUtcNow() + wait : (DateTimeOffset?)null;
        var posted = await questions.Post(new QuestionAsk(context.RunId, context.Node, question, choices, Text(context.Config["default"]), timeoutAt), cancellationToken).ConfigureAwait(false);
        return posted.IsError ? posted.Error : new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal));
    }

    private static bool IsReference(string text) => text.Contains("{{", StringComparison.Ordinal);

    private static string? Text(JsonNode? node) => node is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;
}
