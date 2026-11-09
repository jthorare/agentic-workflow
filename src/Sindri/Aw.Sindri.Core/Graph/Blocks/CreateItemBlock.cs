using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'create_item' block: makes an item through the same create-item handler as 'sindri item add' and hands on its id.</summary>
public sealed class CreateItemBlock(ICreateItemPort items) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["itemId"] = "string" });

    /// <inheritdoc />
    public string Type => "create_item";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("title", BlockFieldType.Text), new("repo", BlockFieldType.Text, Required: false), new("body", BlockFieldType.Text, Required: false), new("linearIssue", BlockFieldType.Text, Required: false)]);

    /// <inheritdoc />
    public string Example => """{"title":"Fix {{triage.summary}}","repo":"web","body":"{{triage.details}}"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        _ = items;
        throw new NotSupportedException("create_item is not written yet");
    }
}
