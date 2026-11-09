using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'notify' block: writes a Needs-you row through <see cref="INotifyPort"/>.</summary>
public sealed class NotifyBlock(INotifyPort notify) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <inheritdoc />
    public string Type => "notify";

    /// <inheritdoc />
    public string Title => "Send notification";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Outputs;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("title", BlockFieldType.Text), new("body", BlockFieldType.Text, Required: false)]);

    /// <inheritdoc />
    public string Example => """{"title":"Drifted","body":"{{agent.summary}}"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var title = Text(context.Config["title"]);
        if (string.IsNullOrWhiteSpace(title))
        {
            return SindriErrors.GraphBlockFailed.With("notify needs a non-empty 'title'");
        }

        var posted = await notify.Post(context.RunId, context.Node, title, Text(context.Config["body"]), cancellationToken).ConfigureAwait(false);
        return posted.IsError ? posted.Error : new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal));
    }

    private static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
        _ => node.ToJsonString(),
    };
}
