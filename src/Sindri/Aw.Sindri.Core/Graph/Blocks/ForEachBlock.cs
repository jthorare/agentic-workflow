using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'for_each' block: takes a list and checks it fits; the engine then runs the nodes up to the matching 'collect' once per item, in batches of 'batchSize'
/// (each batch is a child workflow, so the run's history grows by batches, not by items). Inside the loop '{{item}}' is the item and '{{itemIndex}}' its position counted from 1.
/// </summary>
/// <param name="limits">Where the defaults for 'maxItems' and 'batchSize' come from.</param>
public sealed class ForEachBlock(IForEachLimitSource limits) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["count"] = "number", ["batchSize"] = "number" });

    /// <inheritdoc />
    public string Type => "for_each";

    /// <inheritdoc />
    public string Title => "For each";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Logic;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("items", BlockFieldType.Text), new("maxItems", BlockFieldType.Number, Required: false), new("batchSize", BlockFieldType.Number, Required: false)]);

    /// <inheritdoc />
    public string Example => """{"items":"{{search.results}}","maxItems":100}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Config["items"] is not JsonArray items)
        {
            return Failed("'items' must be a list; point it at an array output such as {{search.results}}");
        }

        var defaults = await limits.Current(cancellationToken).ConfigureAwait(false);
        var asked = Whole(context.Config["maxItems"]);
        var maxItems = Capped(asked, defaults.MaxItems);
        var batchSize = Whole(context.Config["batchSize"]) ?? defaults.BatchSize;
        if (maxItems < 1 || asked > ForEachLimits.MaxItemsCeiling)
        {
            return Failed($"'maxItems' must be from 1 to {ForEachLimits.MaxItemsCeiling.ToString(CultureInfo.InvariantCulture)}, not {(asked ?? maxItems).ToString(CultureInfo.InvariantCulture)}");
        }

        if (batchSize is < 1 or > ForEachLimits.BatchSizeCeiling)
        {
            return Failed($"'batchSize' must be from 1 to {ForEachLimits.BatchSizeCeiling.ToString(CultureInfo.InvariantCulture)}, not {batchSize.ToString(CultureInfo.InvariantCulture)}");
        }

        if (items.Count > maxItems)
        {
            return Failed(TooMany(items.Count, maxItems, asked, defaults.MaxItems));
        }

        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            ["count"] = JsonValue.Create(items.Count),
            ["batchSize"] = JsonValue.Create(batchSize),
        });
    }

    // why: the profile's maxItems is the cap; a node may lower it, never raise it
    private static int Capped(int? asked, int cap) => asked is { } own && own >= 1 ? Math.Min(own, cap) : asked ?? cap;

    private static string TooMany(int count, int maxItems, int? asked, int cap)
    {
        var raised = asked > cap ? $" (the profile's workflow.forEach.maxItems {cap.ToString(CultureInfo.InvariantCulture)} caps it; a node can lower it, not raise it)" : string.Empty;
        return $"the list has {count.ToString(CultureInfo.InvariantCulture)} items, more than maxItems {maxItems.ToString(CultureInfo.InvariantCulture)}{raised}; filter the list first";
    }

    private static int? Whole(JsonNode? node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<int>(out var whole) ? whole : null;

    private static Error Failed(string message) => SindriErrors.GraphBlockFailed.With(message);
}
