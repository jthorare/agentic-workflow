using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Expressions;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'condition' block: evaluates 'when' and leaves by 'true' or 'false' only.</summary>
public sealed class ConditionBlock : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["result"] = "boolean" }, ["true", "false"]);

    /// <inheritdoc />
    public string Type => "condition";

    /// <inheritdoc />
    public string Title => "If";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Logic;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("when", BlockFieldType.Text)]);

    /// <inheritdoc />
    public string Example => """{"when":"agent.drifted"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        // why: the raw text, because interpolation would turn a '{{a.b}}' into text before the expression sees it
        var text = context.RawConfig["when"] is JsonValue value && value.TryGetValue<string>(out var when) ? when : null;
        if (text is null)
        {
            return Failed("the condition has no 'when' text");
        }

        if (!ConditionExpression.TryParse(text, out var expression, out var error) || expression is null)
        {
            return Failed($"the condition can't be read: {error}");
        }

        var result = expression.Evaluate(context.Resolve);
        return ValueTask.FromResult<Result<BlockResult>>(new BlockResult(
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["result"] = JsonValue.Create(result) },
            [result ? "true" : "false"]));
    }

    private static ValueTask<Result<BlockResult>> Failed(string message) => ValueTask.FromResult<Result<BlockResult>>(SindriErrors.GraphBlockFailed.With(message));
}
