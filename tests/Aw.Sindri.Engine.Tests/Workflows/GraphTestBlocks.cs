using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;

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
    public IEnumerable<IGraphBlock> All() => [new Echo(this), new Fail(this), new Hold(this), new Big(), new CommitProbe(this), new Pass(), new FailOnTwo()];

    /// <summary>An in-memory 'workflow_seen' for the real 'only_new' block.</summary>
    internal sealed class MemorySeen : IGraphSeenStore
    {
        private readonly ConcurrentDictionary<(string Workflow, string Key), byte> _seen = new();

        /// <summary>How many keys are remembered.</summary>
        public int Count => _seen.Count;

        public ValueTask<Result<IReadOnlySet<string>>> Seen(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success<IReadOnlySet<string>>(keys.Where(k => _seen.ContainsKey((workflow.Value, k))).ToHashSet(StringComparer.Ordinal)));

        public ValueTask<Result<Unit>> Record(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken)
        {
            foreach (var key in keys)
            {
                _ = _seen.TryAdd((workflow.Value, key), 0);
            }

            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    private abstract class Base(string type) : IGraphBlock
    {
        public string Type => type;

        public string Title => type;

        public BlockGroup Group => BlockGroup.Logic;

        public bool IsTrigger => false;

        public BlockSchema Config { get; } = new([]);

        public string Example => "{}";

        public BlockOutputs OutputsFor(JsonObject config) => new(new Dictionary<string, string>(StringComparer.Ordinal) { ["node"] = "string" });

        public abstract ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken);

        public virtual ValueTask<Result<Unit>> Commit(BlockContext context, BlockResult result, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);

        protected static Result<BlockResult> Ok(BlockContext context) =>
            new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["node"] = JsonValue.Create(context.Node.Value) });
    }

    /// <summary>Logs 'exec' and, when the node succeeded, 'commit' (the block's after-success hook); config 'failCommit' true makes the commit fail.</summary>
    private sealed class CommitProbe(GraphTestBlocks owner) : Base("commit_probe")
    {
        public override ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
        {
            owner._log.Enqueue($"exec {context.RunId.Value}/{context.Node.Value}");
            return context.Config["failExecute"] is JsonValue { } fail && fail.TryGetValue<bool>(out var failed) && failed
                ? ValueTask.FromResult<Result<BlockResult>>(SindriErrors.GraphBlockFailed.With("execute failed"))
                : ValueTask.FromResult(Ok(context));
        }

        public override ValueTask<Result<Unit>> Commit(BlockContext context, BlockResult result, CancellationToken cancellationToken)
        {
            owner._log.Enqueue($"commit {context.RunId.Value}/{context.Node.Value}");
            return ValueTask.FromResult<Result<Unit>>(context.Config["failCommit"] is JsonValue { } fail && fail.TryGetValue<bool>(out var failed) && failed ? SindriErrors.GraphBlockFailed.With("commit failed") : Unit.Value);
        }
    }

    // why: hands on its interpolated 'value', so a loop test can see the item a body node was given
    private sealed class Pass() : Base("pass")
    {
        public override ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<BlockResult>>(new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["value"] = context.Config["value"]?.DeepClone() }));
    }

    // why: fails only when its interpolated 'value' is 2, so a loop test can fail one item and let the other pass
    private sealed class FailOnTwo() : Base("fail_on_two")
    {
        public override ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) =>
            string.Equals(context.Config["value"]?.ToString(), "2", StringComparison.Ordinal)
                ? ValueTask.FromResult<Result<BlockResult>>(SindriErrors.GraphBlockFailed.With("boom"))
                : ValueTask.FromResult(Ok(context));
    }

    private sealed class Big() : Base("big")
    {
        public override ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<BlockResult>>(new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["data"] = JsonValue.Create(new string('x', 70_000)) }));
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
