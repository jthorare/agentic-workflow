using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'only_new' block: passes on the items of a list that this workflow has not passed on before, keyed by a field of each item. It records the keys only when the whole run
/// succeeded (<see cref="Commit"/>), so a run that fails anywhere leaves its items unseen and they come back on the next run. The keys are remembered per workflow, not per run.
/// </summary>
public sealed class OnlyNewBlock(IGraphSeenStore seen) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["items"] = "array", ["count"] = "number", ["skipped"] = "number" });

    /// <inheritdoc />
    public string Type => "only_new";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("items", BlockFieldType.Text), new("key", BlockFieldType.Text, Required: false)]);

    /// <inheritdoc />
    public string Example => """{"items":"{{search.results}}","key":"id"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var read = Read(context);
        if (read.IsError)
        {
            return read.Error;
        }

        var (workflow, total, items) = read.Value;
        var checkedAgainst = await seen.Seen(workflow, [.. items.Select(static i => i.Key)], cancellationToken).ConfigureAwait(false);
        if (checkedAgainst.IsError)
        {
            return checkedAgainst.Error;
        }

        var unseen = items.Where(i => !checkedAgainst.Value.Contains(i.Key)).ToList();
        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            ["items"] = new JsonArray([.. unseen.Select(static i => i.Item?.DeepClone())]),
            ["count"] = unseen.Count,
            ["skipped"] = total - unseen.Count,
        });
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Commit(BlockContext context, BlockResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        var read = Read(context, result.Outputs.GetValueOrDefault("items") as JsonArray);
        if (read.IsError)
        {
            return read.Error;
        }

        var (workflow, _, items) = read.Value;
        return await seen.Record(workflow, [.. items.Select(static i => i.Key)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The workflow and the distinct keyed items of the list: the node's 'items' config, or <paramref name="passedOn"/> when the commit reads what the node gave.</summary>
    private static Result<(WorkflowId Workflow, int Total, IReadOnlyList<Keyed> Items)> Read(BlockContext context, JsonArray? passedOn = null)
    {
        if (!WorkflowId.TryFrom(ConfigValues.Text(context.Resolve("workflow.id")) ?? string.Empty, out var workflow))
        {
            return SindriErrors.GraphBlockFailed.With("only_new can't tell which workflow it runs in (the run has no workflow id)");
        }

        if ((passedOn ?? context.Config["items"] as JsonArray) is not { } list)
        {
            return SindriErrors.GraphBlockFailed.With("only_new needs 'items' to be a list, such as a reference to a node's list output ({{search.results}})");
        }

        var key = ConfigValues.Text(context.Config["key"]);
        var keyed = new List<Keyed>();
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < list.Count; i++)
        {
            var found = KeyOf(list[i], key, i);
            if (found.IsError)
            {
                return found.Error;
            }

            if (distinct.Add(found.Value))
            {
                keyed.Add(new Keyed(found.Value, list[i]));
            }
        }

        var read = (Workflow: workflow, Total: list.Count, Items: (IReadOnlyList<Keyed>)keyed);
        return read;
    }

    private static Result<string> KeyOf(JsonNode? item, string? path, int position)
    {
        var value = item;
        foreach (var part in (path ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            value = value is JsonObject obj && obj.TryGetPropertyValue(part, out var next) ? next : null;
        }

        return value switch
        {
            null => SindriErrors.GraphBlockFailed.With($"item {position.ToString(CultureInfo.InvariantCulture)} has no '{path}' to key it by (fix: name a field every item has, or leave 'key' out)"),
            JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
            _ => Canonical(value)?.ToJsonString() ?? "null",
        };
    }

    /// <summary>A copy of the node with object keys sorted ordinally, recursively; arrays keep their order.</summary>
    private static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(static p => p.Key, StringComparer.Ordinal).Select(static p => KeyValuePair.Create(p.Key, Canonical(p.Value)))),
        JsonArray arr => new JsonArray([.. arr.Select(static n => Canonical(n))]),
        _ => node?.DeepClone(),
    };

    private sealed record Keyed(string Key, JsonNode? Item);
}
