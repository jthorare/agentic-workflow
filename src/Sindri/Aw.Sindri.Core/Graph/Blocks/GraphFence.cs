using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The test versions of the write ports (spec 2026-11-12 §5.1). Each wrapper passes every call to the live port, unless a test run's recorder is open
/// (<see cref="GraphTestRecorder.Active"/>): then a write is recorded as a "would have" row and nothing is sent. Reads still run for real.
/// </summary>
public static class GraphFence
{
    /// <summary>Wraps the notify port.</summary>
    /// <param name="live">The live port.</param>
    /// <returns>The fenced port.</returns>
    public static INotifyPort Notify(INotifyPort live) => new FencedNotify(live);

    /// <summary>Wraps the create-item port.</summary>
    /// <param name="live">The live port.</param>
    /// <returns>The fenced port.</returns>
    public static ICreateItemPort CreateItem(ICreateItemPort live) => new FencedCreateItem(live);

    /// <summary>Wraps the question port.</summary>
    /// <param name="live">The live port.</param>
    /// <returns>The fenced port.</returns>
    public static IQuestionPort Question(IQuestionPort live) => new FencedQuestion(live);

    /// <summary>Wraps the HTTP port: a GET runs for real, any other method is recorded.</summary>
    /// <param name="live">The live port.</param>
    /// <returns>The fenced port.</returns>
    public static IGraphHttpPort Http(IGraphHttpPort live) => new FencedHttp(live);

    /// <summary>Wraps the memory port: a recall runs for real, a remember and a forget are recorded.</summary>
    /// <param name="live">The live port.</param>
    /// <returns>The fenced port.</returns>
    public static IGraphMemoryPort Memory(IGraphMemoryPort live) => new FencedMemory(live);

    /// <summary>Wraps the seen store: a read runs for real, a record is not written.</summary>
    /// <param name="live">The live store.</param>
    /// <returns>The fenced store.</returns>
    public static IGraphSeenStore Seen(IGraphSeenStore live) => new FencedSeen(live);

    /// <summary>Wraps the model port: a test run asks no model.</summary>
    /// <param name="live">The live port.</param>
    /// <returns>The fenced port.</returns>
    public static IGraphModelPort Model(IGraphModelPort live) => new FencedModel(live);

    /// <summary>Wraps the workflow-call port: a test run starts no callee.</summary>
    /// <param name="live">The live port.</param>
    /// <returns>The fenced port.</returns>
    public static IGraphCallPort Calls(IGraphCallPort live) => new FencedCalls(live);

    /// <summary>Wraps the connection tool port: only a tool marked read-only runs for real; every other tool is recorded.</summary>
    /// <param name="live">The live port.</param>
    /// <returns>The fenced port.</returns>
    public static IConnectionToolPort ConnectionTools(IConnectionToolPort live) => new FencedConnectionTools(live);

    /// <summary>Wraps the toolkit runtime: only a tool marked read-only runs for real; every other tool is recorded.</summary>
    /// <param name="live">The live runtime.</param>
    /// <returns>The fenced runtime.</returns>
    public static IRunningToolkits Toolkits(IRunningToolkits live) => new FencedToolkits(live);

    private static ValueTask<Result<T>> Done<T>(T value) => ValueTask.FromResult<Result<T>>(value);

    private static ItemId MadeUp(string title)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(title));
        return ItemId.From("ITM-" + Convert.ToHexStringLower(hash)[..32]);
    }

    private sealed class FencedNotify(INotifyPort live) : INotifyPort
    {
        public ValueTask<Result<Unit>> Post(WorkflowRunId runId, NodeId node, string title, string? body, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active)
            {
                return live.Post(runId, node, title, body, cancellationToken);
            }

            GraphTestRecorder.Record(body is null ? $"would have: notify '{title}'" : $"would have: notify '{title}' with '{body}'");
            return Done(Unit.Value);
        }
    }

    private sealed class FencedCreateItem(ICreateItemPort live) : ICreateItemPort
    {
        public ValueTask<Result<ItemMade>> Create(ItemToCreate item, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active)
            {
                return live.Create(item, cancellationToken);
            }

            GraphTestRecorder.Record($"would have: create item '{item.Title}'");
            return Done(new ItemMade(MadeUp(item.Title), Warning: null));
        }
    }

    private sealed class FencedQuestion(IQuestionPort live) : IQuestionPort
    {
        public ValueTask<Result<Unit>> Post(QuestionAsk ask, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active)
            {
                return live.Post(ask, cancellationToken);
            }

            GraphTestRecorder.Record($"would have asked: '{ask.Question}'");
            return Done(Unit.Value);
        }
    }

    private sealed class FencedHttp(IGraphHttpPort live) : IGraphHttpPort
    {
        public ValueTask<Result<GraphHttpResponse>> Send(GraphHttpRequest request, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active || string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return live.Send(request, cancellationToken);
            }

            GraphTestRecorder.Record($"would have: {request.Method.ToUpperInvariant()} {request.Url.Host}{request.Url.AbsolutePath}" + (request.Body is null ? string.Empty : $" with '{request.Body}'"));
            return Done(new GraphHttpResponse(200, "{}"));
        }
    }

    private sealed class FencedSeen(IGraphSeenStore live) : IGraphSeenStore
    {
        public ValueTask<Result<IReadOnlySet<string>>> Seen(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken) => live.Seen(workflow, keys, cancellationToken);

        public ValueTask<Result<Unit>> Record(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active)
            {
                return live.Record(workflow, keys, cancellationToken);
            }

            GraphTestRecorder.Record($"would mark seen: {string.Join(", ", keys)}");
            return Done(Unit.Value);
        }
    }

    private sealed class FencedModel(IGraphModelPort live) : IGraphModelPort
    {
        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active)
            {
                return live.Ask(prompt, cancellationToken);
            }

            GraphTestRecorder.Record("model not run");
            return Done("{}");
        }

        public string Untrusted(string source, string text) => live.Untrusted(source, text);
    }

    private sealed class FencedCalls(IGraphCallPort live) : IGraphCallPort
    {
        public ValueTask<int> MaxDepth(CancellationToken cancellationToken) => live.MaxDepth(cancellationToken);

        public ValueTask<Result<GraphCalled>> Start(GraphCall call, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active)
            {
                return live.Start(call, cancellationToken);
            }

            GraphTestRecorder.Record($"would have: start workflow '{call.Target.Value}'");
            return Done(new GraphCalled(WorkflowRunId.Of(call.Target, 1, 1), "succeeded"));
        }

        public ValueTask<Result<GraphCallEnd>> Wait(WorkflowRunId run, TimeSpan timeout, CancellationToken cancellationToken) =>
            GraphTestRecorder.Active ? Done(new GraphCallEnd("succeeded", Outputs: null, Error: null)) : live.Wait(run, timeout, cancellationToken);
    }

    private sealed class FencedMemory(IGraphMemoryPort live) : IGraphMemoryPort
    {
        public ValueTask<Result<MemoryRecalled>> Recall(MemoryToRecall request, CancellationToken cancellationToken) => live.Recall(request, cancellationToken);

        public ValueTask<Result<MemoryRemembered>> Remember(MemoryToRemember request, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active)
            {
                return live.Remember(request, cancellationToken);
            }

            GraphTestRecorder.Record($"would remember: '{request.Fact}' in '{request.Memory}'");
            return Done(new MemoryRemembered(Key: "test", Action: "would remember"));
        }

        public ValueTask<Result<int>> Forget(MemoryToForget request, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active)
            {
                return live.Forget(request, cancellationToken);
            }

            GraphTestRecorder.Record($"would forget in '{request.Memory}'");
            return Done(0);
        }
    }

    private sealed class FencedConnectionTools(IConnectionToolPort live) : IConnectionToolPort
    {
        public ValueTask<Result<IReadOnlyList<ConnectionTool>>> ListTools(string connection, CancellationToken cancellationToken) => live.ListTools(connection, cancellationToken);

        public async ValueTask<Result<string>> Call(string connection, string tool, string argumentsJson, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active)
            {
                return await live.Call(connection, tool, argumentsJson, cancellationToken).ConfigureAwait(false);
            }

            var tools = await live.ListTools(connection, cancellationToken).ConfigureAwait(false);
            if (tools.IsSuccess && tools.Value.Any(t => string.Equals(t.Name, tool, StringComparison.Ordinal) && t.ReadOnly))
            {
                return await live.Call(connection, tool, argumentsJson, cancellationToken).ConfigureAwait(false);
            }

            GraphTestRecorder.Record($"would have: call {connection}/{tool} with {argumentsJson}");   // unknown = write
            return "{}";
        }
    }

    private sealed class FencedToolkits(IRunningToolkits live) : IRunningToolkits
    {
        public IReadOnlyList<Toolkit> Toolkits() => live.Toolkits();

        public async ValueTask<Result<string>> CallTool(string toolkit, string tool, string argumentsJson, string caller, CancellationToken cancellationToken)
        {
            if (!GraphTestRecorder.Active || ReadOnly(toolkit, tool))
            {
                return await live.CallTool(toolkit, tool, argumentsJson, caller, cancellationToken).ConfigureAwait(false);
            }

            GraphTestRecorder.Record($"would have: call tool {toolkit}/{tool} with {argumentsJson}");   // unknown = write
            return "{}";
        }

        private bool ReadOnly(string toolkit, string tool) =>
            live.Toolkits().Any(k => string.Equals(k.Name, toolkit, StringComparison.Ordinal) && k.Manifest is { } m && m.Tools.Any(t => string.Equals(t.Name, tool, StringComparison.Ordinal) && t.ReadOnly));
    }
}
