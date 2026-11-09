using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'http' block: one request to a host on the profile's 'workflow.http.hosts', unauthenticated until connections exist. It fails the node when the status is not in 'expect'
/// (default any 2xx) and hands on the status, the JSON body when the answer is JSON, and the text.
/// </summary>
public sealed class HttpBlock(IGraphHttpPort http) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["status"] = "number",
        ["body"] = "any?",
        ["text"] = "string",
    });

    /// <inheritdoc />
    public string Type => "http";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("url", BlockFieldType.Text), new("method", BlockFieldType.Text, Required: false), new("headers", BlockFieldType.Map, Required: false),
        new("body", BlockFieldType.Map, Required: false), new("expect", BlockFieldType.Text, Required: false)]);

    /// <inheritdoc />
    public string Example => """{"url":"https://api.example.com/items","method":"POST","body":{"name":"{{triage.summary}}"},"expect":"200,201"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        _ = http;
        throw new NotSupportedException("http is not written yet");
    }
}
