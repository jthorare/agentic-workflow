using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'schedule' trigger: a cron and a timezone; hands on the tick it fired for.</summary>
public sealed class ScheduleBlock : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["scheduledFor"] = "string" });

    /// <inheritdoc />
    public string Type => "schedule";

    /// <inheritdoc />
    public string Title => "Schedule";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Triggers;

    /// <inheritdoc />
    public bool IsTrigger => true;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("cron", BlockFieldType.Text), new("timezone", BlockFieldType.Text, Required: false)]);

    /// <inheritdoc />
    public string Example => """{"cron":"0 7 * * 1-5","timezone":"America/Los_Angeles"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult<Result<BlockResult>>(new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["scheduledFor"] = context.Resolve("trigger.scheduledFor") }));
    }
}
