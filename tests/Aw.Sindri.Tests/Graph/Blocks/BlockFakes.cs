using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Tests.Graph.Blocks;

internal static class BlockFakes
{
    public static BlockContext Context(JsonObject config, Dictionary<string, JsonNode?>? values = null, string node = "n1")
    {
        var known = values ?? new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        return new BlockContext(WorkflowRunId.From("wf@1:1"), NodeId.From(node), config, (JsonObject)config.DeepClone(), reference => known.GetValueOrDefault(reference)?.DeepClone());
    }

    public sealed class RecordingMemoryPort : IGraphMemoryPort
    {
        public List<MemoryToRecall> Recalls { get; } = [];

        public List<MemoryToRemember> Writes { get; } = [];

        public List<MemoryToForget> Forgets { get; } = [];

        public MemoryRecalled Recalled { get; init; } = new([], "semantic");

        public MemoryRemembered Remembered { get; init; } = new("mf_1", "added");

        public int Forgotten { get; init; }

        public Error? Fail { get; init; }

        public ValueTask<Result<MemoryRecalled>> Recall(MemoryToRecall request, CancellationToken cancellationToken)
        {
            Recalls.Add(request);
            return ValueTask.FromResult<Result<MemoryRecalled>>(Fail is { } error ? error : Recalled);
        }

        public ValueTask<Result<MemoryRemembered>> Remember(MemoryToRemember request, CancellationToken cancellationToken)
        {
            Writes.Add(request);
            return ValueTask.FromResult<Result<MemoryRemembered>>(Fail is { } error ? error : Remembered);
        }

        public ValueTask<Result<int>> Forget(MemoryToForget request, CancellationToken cancellationToken)
        {
            Forgets.Add(request);
            return ValueTask.FromResult<Result<int>>(Fail is { } error ? error : Forgotten);
        }
    }

    public sealed class RecordingNotifyPort : INotifyPort
    {
        public List<(string RunId, string Node, string Title, string? Body)> Posted { get; } = [];

        public Error? Fail { get; init; }

        public ValueTask<Result<Unit>> Post(WorkflowRunId runId, NodeId node, string title, string? body, CancellationToken cancellationToken)
        {
            Posted.Add((runId.Value, node.Value, title, body));
            return ValueTask.FromResult<Result<Unit>>(Fail is { } error ? error : Unit.Value);
        }
    }

    public sealed class RecordingQuestionPort : IQuestionPort
    {
        public List<QuestionAsk> Posted { get; } = [];

        public Error? Fail { get; init; }

        public ValueTask<Result<Unit>> Post(QuestionAsk ask, CancellationToken cancellationToken)
        {
            Posted.Add(ask);
            return ValueTask.FromResult<Result<Unit>>(Fail is { } error ? error : Unit.Value);
        }
    }

    public sealed class ScriptedModelPort(string answer) : IGraphModelPort
    {
        public List<string> Prompts { get; } = [];

        public string Untrusted(string source, string text) => $"<untrusted>{text}</untrusted>";

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return ValueTask.FromResult<Result<string>>(answer);
        }
    }
}
