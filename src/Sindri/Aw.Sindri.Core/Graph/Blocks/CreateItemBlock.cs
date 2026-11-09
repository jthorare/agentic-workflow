using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'create_item' block: makes an item through the same create-item handler as 'sindri item add' and hands on its id.</summary>
public sealed class CreateItemBlock(ICreateItemPort items) : IGraphBlock
{
    private const string LinearPrefix = "linear:issue/";

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["itemId"] = "string" });

    /// <inheritdoc />
    public string Type => "create_item";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("title", BlockFieldType.Text),
        new("repo", BlockFieldType.Text, Required: false),
        new("body", BlockFieldType.Text, Required: false),
        new("linearIssue", BlockFieldType.Text, Required: false),
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
        var made = await items.Create(title, string.IsNullOrWhiteSpace(repo) ? null : repo.Trim(), ConfigValues.Text(context.Config["body"]), link.Value, cancellationToken).ConfigureAwait(false);
        return made.IsError ? made.Error : new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["itemId"] = made.Value });
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
