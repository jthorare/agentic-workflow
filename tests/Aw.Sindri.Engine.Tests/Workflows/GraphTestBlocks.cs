using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>Three blocks for the walk tests: 'echo' succeeds, 'fail' fails, 'hold' waits until the test lets it go. Each logs what ran, in order.</summary>
internal sealed class GraphTestBlocks
{
    private readonly ConcurrentQueue<string> _log = new();
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>What ran, in order: '&lt;run&gt;/&lt;node&gt;' for echo and fail, 'start &lt;run&gt;/&lt;node&gt;' and 'done &lt;run&gt;/&lt;node&gt;' for hold.</summary>
    public IReadOnlyList<string> Log => [.. _log];

    /// <summary>Completes when a hold block has started.</summary>
    public Task Entered => _entered.Task;

    /// <summary>Lets every hold block, now and later, finish.</summary>
    public void Release() => _release.TrySetResult();

    /// <summary>The three blocks.</summary>
    /// <returns>Echo, fail and hold.</returns>
    public IEnumerable<IGraphBlock> All() => [new Echo(this), new Fail(this), new Hold(this)];

    private abstract class Base(string type) : IGraphBlock
    {
        public string Type => type;

        public bool IsTrigger => false;

        public BlockSchema Config { get; } = new([]);

        public string Example => "{}";

        public BlockOutputs OutputsFor(JsonObject config) => new(new Dictionary<string, string>(StringComparer.Ordinal) { ["node"] = "string" });

        public abstract ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken);

        protected static Result<BlockResult> Ok(BlockContext context) =>
            new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["node"] = JsonValue.Create(context.Node.Value) });
    }

    private sealed class Echo(GraphTestBlocks owner) : Base("echo")
    {
        public override ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
        {
            owner._log.Enqueue($"{context.RunId.Value}/{context.Node.Value}");
            return ValueTask.FromResult(Ok(context));
        }
    }

    private sealed class Fail(GraphTestBlocks owner) : Base("fail")
    {
        public override ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
        {
            owner._log.Enqueue($"{context.RunId.Value}/{context.Node.Value}");
            return ValueTask.FromResult<Result<BlockResult>>(SindriErrors.GraphBlockFailed.With("boom"));
        }
    }

    private sealed class Hold(GraphTestBlocks owner) : Base("hold")
    {
        public override async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
        {
            owner._log.Enqueue($"start {context.RunId.Value}/{context.Node.Value}");
            _ = owner._entered.TrySetResult();
            await owner._release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            owner._log.Enqueue($"done {context.RunId.Value}/{context.Node.Value}");
            return Ok(context);
        }
    }
}
