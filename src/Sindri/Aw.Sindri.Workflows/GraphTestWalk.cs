using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The test branch of a block activity (spec 2026-11-12 §3.2, §5.1): in a test run the ports are fenced (<see cref="GraphFence"/>), a 'wait' finishes at once and says "would wait ...",
/// and an 'ask_owner' takes its default or its first choice. Every row a fenced port recorded is added to the node's output as 'would'.
/// </summary>
internal static class GraphTestWalk
{
    /// <summary>True for the blocks the walk would park on, so a test run's node is not parked again after the block answered.</summary>
    /// <param name="type">The block type.</param>
    /// <returns>True for 'wait' and 'ask_owner'.</returns>
    public static bool Parks(string type) => string.Equals(type, "wait", StringComparison.Ordinal) || string.Equals(type, "ask_owner", StringComparison.Ordinal);

    /// <summary>Writes a test node's output so a "would have" row reads as it was recorded: an apostrophe stays an apostrophe, not \u0027.</summary>
    public static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Runs the block; in a test run with a recorder open, then turns the result into what a test node stores.</summary>
    /// <param name="block">The block.</param>
    /// <param name="context">The node's context.</param>
    /// <param name="test">True for a test run.</param>
    /// <param name="cancellationToken">Cancels the execution.</param>
    /// <returns>The block's result, or the test version of it.</returns>
    public static async ValueTask<Result<BlockResult>> Execute(IGraphBlock block, BlockContext context, bool test, CancellationToken cancellationToken)
    {
        if (!test)
        {
            return await block.Execute(context, cancellationToken).ConfigureAwait(false);
        }

        var recorded = GraphTestRecorder.Begin();
        try
        {
            var result = await block.Execute(context, cancellationToken).ConfigureAwait(false);
            return result.IsError ? result : Apply(block.Type, context.Config, result.Value, recorded);
        }
        finally
        {
            GraphTestRecorder.End();
        }
    }

    private static BlockResult Apply(string type, JsonObject config, BlockResult result, IList<string> recorded)
    {
        var outputs = result.Outputs.ToDictionary(static p => p.Key, static p => p.Value?.DeepClone(), StringComparer.Ordinal);
        var branches = result.Branches;
        if (string.Equals(type, "wait", StringComparison.Ordinal))
        {
            outputs["waitedSeconds"] = 0;
            recorded.Add(WouldWait(config));
        }
        else if (string.Equals(type, "ask_owner", StringComparison.Ordinal))
        {
            var choices = AskOwnerBlock.ChoicesOf(config);
            var answer = Text(config["default"]) ?? (choices.Count > 0 ? choices[0] : "ok");
            var resolved = AskOwnerBlock.Resolve(config, answer, "test");
            foreach (var (name, value) in resolved.Outputs)
            {
                outputs[name] = value?.DeepClone();
            }

            branches = [resolved.Branch];
        }

        if (recorded.Count > 0)
        {
            var would = new JsonArray();
            foreach (var line in recorded)
            {
                would.Add(line);
            }

            outputs["would"] = would;
        }

        return new BlockResult(outputs, branches);
    }

    private static string WouldWait(JsonObject config) =>
        Text(config["duration"]) is { } duration ? $"would wait {duration}" : $"would wait until {Text(config["until"])}";

    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
}
