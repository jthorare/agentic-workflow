using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>Fakes for the flow blocks (agent-first PR 9 part C): the seen store, the item, call and http ports.</summary>
internal static class FlowFakes
{
    /// <summary>Two workflow ids ("WF-" and 32 lowercase hex digits).</summary>
    public const string WorkflowA = "WF-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public const string WorkflowB = "WF-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    public sealed class MemorySeenStore : IGraphSeenStore
    {
        private readonly HashSet<(string Workflow, string Key)> _seen = [];

        public int Writes { get; private set; }

        public Error? FailWrites { get; init; }

        public ValueTask<Result<IReadOnlySet<string>>> Seen(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success<IReadOnlySet<string>>(keys.Where(k => _seen.Contains((workflow.Value, k))).ToHashSet(StringComparer.Ordinal)));

        public ValueTask<Result<Unit>> Record(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken)
        {
            Writes++;
            if (FailWrites is { } error)
            {
                return ValueTask.FromResult<Result<Unit>>(error);
            }

            foreach (var key in keys)
            {
                _ = _seen.Add((workflow.Value, key));
            }

            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    public sealed class RecordingItemPort : ICreateItemPort
    {
        public List<(string Title, string? Repo, string? Body, string? Link)> Created { get; } = [];

        public Error? Fail { get; init; }

        public string? Warning { get; init; }

        public List<string> Runs { get; } = [];

        public ValueTask<Result<ItemMade>> Create(ItemToCreate item, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(item);
            Runs.Add(item.Run.Value);
            Created.Add((item.Title, item.Repo, item.Body, item.LinearIssue));
            return ValueTask.FromResult<Result<ItemMade>>(Fail is { } error ? error : new ItemMade(Aw.Sindri.Core.Ids.ItemId.From($"ITM-{Created.Count:x32}"), Warning));
        }
    }

    public sealed class ScriptedCallPort(int maxDepth = 3) : IGraphCallPort
    {
        public List<GraphCall> Started { get; } = [];

        public List<(WorkflowRunId Run, TimeSpan Timeout)> Waited { get; } = [];

        public string StartStatus { get; init; } = "started";

        public GraphCallEnd End { get; init; } = new("succeeded", new JsonObject { ["answer"] = 42 }, Error: null);

        public ValueTask<int> MaxDepth(CancellationToken cancellationToken) => ValueTask.FromResult(maxDepth);

        public ValueTask<Result<GraphCalled>> Start(GraphCall call, CancellationToken cancellationToken)
        {
            Started.Add(call);
            return ValueTask.FromResult<Result<GraphCalled>>(new GraphCalled(WorkflowRunId.Of(call.Target, 1, 100_000_001), StartStatus));
        }

        public ValueTask<Result<GraphCallEnd>> Wait(WorkflowRunId run, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Waited.Add((run, timeout));
            return ValueTask.FromResult<Result<GraphCallEnd>>(End);
        }
    }

    public sealed class ScriptedResultPort : IGraphResultPort
    {
        public ValueTask<Result<GraphRunResult>> Read(WorkflowSlug workflow, WorkflowRunId? run, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<GraphRunResult>>(new GraphRunResult(WorkflowRunId.Of(workflow, 1, 100_000_001), "succeeded", FinishedAt: null, Outputs: null));
    }

    public sealed class ScriptedHttpPort(GraphHttpResponse? answer = null, Error? error = null) : IGraphHttpPort
    {
        public List<GraphHttpRequest> Sent { get; } = [];

        public ValueTask<Result<GraphHttpResponse>> Send(GraphHttpRequest request, CancellationToken cancellationToken)
        {
            Sent.Add(request);
            return ValueTask.FromResult<Result<GraphHttpResponse>>(error is { } failed ? failed : answer ?? new GraphHttpResponse(200, "{}"));
        }
    }

    /// <summary>An <see cref="HttpMessageHandler"/> that answers from a script and never touches the network; it records every request it saw.</summary>
    public sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Url)> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((request.Method, request.RequestUri!));
            return Task.FromResult(answer(request));
        }

        public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

        public static HttpResponseMessage Redirect(string to) => new(HttpStatusCode.Found) { Headers = { Location = new Uri(to) } };
    }

    public static Func<CancellationToken, ValueTask<IReadOnlyList<string>>> Hosts(params string[] hosts) => _ => ValueTask.FromResult<IReadOnlyList<string>>(hosts);
}
