using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'wait' block: says when the node wakes ('duration' like '15m', or 'until' a time in a 'timezone'). The block only does the sum; the workflow sleeps on a Temporal timer for the
/// 'waitedSeconds' it hands on, so nothing polls, and a cancel ends the sleep.
/// </summary>
/// <param name="clock">The clock.</param>
public sealed class WaitBlock(TimeProvider clock) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["wakeAt"] = "string", ["waitedSeconds"] = "number" });

    /// <inheritdoc />
    public string Type => "wait";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("duration", BlockFieldType.Text, Required: false),
        new("until", BlockFieldType.Text, Required: false),
        new("timezone", BlockFieldType.Text, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"until":"07:00","timezone":"America/Los_Angeles"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config) => WaitSchedule.Check(config);

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var now = clock.GetUtcNow();
        var wake = WaitSchedule.WakeAt(context.Config, now);
        if (wake.IsError)
        {
            return ValueTask.FromResult<Result<BlockResult>>(wake.Error);
        }

        var wait = wake.Value > now ? wake.Value - now : TimeSpan.Zero;
        return ValueTask.FromResult<Result<BlockResult>>(new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            ["wakeAt"] = wake.Value.ToString("O", CultureInfo.InvariantCulture),
            ["waitedSeconds"] = Math.Round(wait.TotalSeconds, 3, MidpointRounding.AwayFromZero),
        }));
    }
}
