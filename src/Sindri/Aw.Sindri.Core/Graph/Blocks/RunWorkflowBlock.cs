using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Guards;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'run_workflow' block: starts another workflow's active revision, with a payload, and either goes on at once or (with 'wait') until the called run ends. The call is guarded
/// by <see cref="CallDepth"/>: at most 'workflow.callDepth' deep, and never the run's own workflow.
/// </summary>
public sealed class RunWorkflowBlock(IGraphCallPort calls) : IGraphBlock
{
    /// <summary>The longest 'wait' waits when 'waitSeconds' is not set.</summary>
    public const int DefaultWaitSeconds = 600;

    private const int MaxWaitSeconds = 3500;   // why: a node's activity is cut at one hour

    // why: 'outputs' is the called run's WorkflowRun.Outputs: an object keyed by node id with each succeeded node's outputs (an array per item for a node that ran per item), null when over 64 KB
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["runId"] = "string",
        ["status"] = "string",
        ["outputs"] = "any?",
    });

    /// <inheritdoc />
    public string Type => "run_workflow";

    /// <inheritdoc />
    public string Title => "Run workflow";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Logic;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("workflow", BlockFieldType.Text, Caption: "Workflow to run"),
        new("payload", BlockFieldType.Map, Required: false, Caption: "Data to send"),
        new("wait", BlockFieldType.Boolean, Required: false, Caption: "Wait for it to finish"),
        new("waitSeconds", BlockFieldType.Number, Required: false, Caption: "Wait (seconds)"),
    ]);

    /// <inheritdoc />
    public string Example => """{"workflow":"triage","payload":{"pr":"{{trigger.payload.pr}}"},"wait":true}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = ConfigValues.Text(context.Config["workflow"]) ?? string.Empty;
        if (!WorkflowSlug.TryFrom(text, out var target))
        {
            return SindriErrors.GraphBlockFailed.With($"'{text}' is not a workflow slug (lowercase letters, digits and '-'; fix: use the name 'sindri workflow list' shows)");
        }

        var origin = new CallOrigin(context.RunId.Value, ConfigValues.Int(context.Resolve("trigger.callDepth")) ?? 0, ConfigValues.Text(context.Resolve("trigger.parentRun")))
        {
            Ancestors = Ancestors(context.Resolve("trigger.ancestors")),
        };
        var max = await calls.MaxDepth(cancellationToken).ConfigureAwait(false);
        var depth = CallDepth.Next(origin, target.Value, max);
        if (depth.IsError)
        {
            return depth.Error;
        }

        var payload = (context.Config["payload"] as JsonObject)?.DeepClone() as JsonObject;
        var index = ConfigValues.Int(context.Resolve("itemIndex")) ?? 0;
        var called = await calls.Start(new GraphCall(context.RunId, context.Node, index, target, payload, depth.Value) { Ancestors = CallDepth.AncestorsOf(origin, max) }, cancellationToken).ConfigureAwait(false);
        if (called.IsError)
        {
            return called.Error;
        }

        var waits = ConfigValues.Bool(context.Config["wait"]) == true && !string.Equals(called.Value.Status, "skipped", StringComparison.Ordinal);
        return waits ? await Wait(context, called.Value, cancellationToken).ConfigureAwait(false) : Outputs(called.Value.Run, called.Value.Status, outputs: null);
    }

    private static List<string> Ancestors(JsonNode? node) =>
        node is JsonArray list ? [.. list.Select(static item => ConfigValues.Text(item)).OfType<string>()] : [];

    private async ValueTask<Result<BlockResult>> Wait(BlockContext context, GraphCalled called, CancellationToken cancellationToken)
    {
        var seconds = Math.Clamp(ConfigValues.Int(context.Config["waitSeconds"]) ?? DefaultWaitSeconds, 1, MaxWaitSeconds);
        var end = await calls.Wait(called.Run, TimeSpan.FromSeconds(seconds), cancellationToken).ConfigureAwait(false);
        if (end.IsError)
        {
            return end.Error;
        }

        return string.Equals(end.Value.Status, "succeeded", StringComparison.Ordinal)
            ? Outputs(called.Run, end.Value.Status, end.Value.Outputs)
            : SindriErrors.GraphBlockFailed.With($"the called run {called.Run.Value} {end.Value.Status}{(end.Value.Error is { } why ? ": " + why : string.Empty)}");
    }

    private static BlockResult Outputs(WorkflowRunId run, string status, JsonNode? outputs) => new(new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
    {
        ["runId"] = run.Value,
        ["status"] = status,
        ["outputs"] = outputs?.DeepClone(),
    });
}
