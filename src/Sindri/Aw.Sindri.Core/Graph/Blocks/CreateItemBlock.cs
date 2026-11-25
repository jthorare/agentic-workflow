using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'create_item' block: makes an item through the same create-item handler as 'sindri item add' and hands on its id (and a 'warning' when something after the item wasn't written).</summary>
public sealed class CreateItemBlock(ICreateItemPort items) : IGraphBlock
{
    private const string LinearPrefix = "linear:issue/";

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["itemId"] = "string", ["warning"] = "string?" });

    /// <inheritdoc />
    public string Type => "create_item";

    /// <inheritdoc />
    public string Title => "Create item";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Outputs;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("title", BlockFieldType.Text, Caption: "Title"),
        new("repo", BlockFieldType.Text, Required: false, Caption: "Repository"),
        new("body", BlockFieldType.Text, Required: false, Caption: "Details"),
        new("linearIssue", BlockFieldType.Text, Required: false, Caption: "Linear issue"),
    ]);

    /// <inheritdoc />
    public string Example => """{"title":"Fix {{triage.summary}}","repo":"web","body":"{{triage.details}}"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var title = ConfigValues.Text(context.Config["title"])?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return SindriErrors.GraphBlockFailed.With("create_item needs a non-empty 'title'");
        }

        var link = Link(ConfigValues.Text(context.Config["linearIssue"]));
        if (link.IsError)
        {
            return link.Error;
        }

        var repo = ConfigValues.Text(context.Config["repo"]);
        var made = await items.Create(new ItemToCreate(context.RunId, title, string.IsNullOrWhiteSpace(repo) ? null : repo.Trim(), ConfigValues.Text(context.Config["body"]), link.Value), cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var outputs = new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["itemId"] = made.Value.ItemId.Value };
        if (made.Value.Warning is { } warning)
        {
            outputs["warning"] = warning;   // only when something wasn't written: the item exists, so the node still succeeds
        }

        return new BlockResult(outputs);
    }

    /// <summary>The record key of a 'linearIssue' value: a whole 'linear:issue/&lt;id&gt;' key as it is, a bare id with the prefix; null when none.</summary>
    private static Result<string?> Link(string? issue)
    {
        if (string.IsNullOrWhiteSpace(issue))
        {
            return (string?)null;
        }

        var key = issue.Contains(':', StringComparison.Ordinal) ? issue.Trim() : LinearPrefix + issue.Trim();
        return RecordKey.TryFrom(key, out _)
            ? key
            : SindriErrors.GraphBlockFailed.With($"'linearIssue' must be a Linear issue id or a key like {LinearPrefix}<id> (letters, digits and '-')");
    }
}
