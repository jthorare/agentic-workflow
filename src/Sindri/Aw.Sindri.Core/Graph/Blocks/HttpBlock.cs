using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'http' block: one request to a host on the profile's 'workflow.http.hosts', unauthenticated until connections exist. It fails the node when the status is not in 'expect'
/// (default any 2xx) and hands on the status, the JSON body when the answer is JSON, and the text.
/// </summary>
public sealed class HttpBlock(IGraphHttpPort http) : IGraphBlock
{
    private static readonly string[] _methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

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
        new("url", BlockFieldType.Text),
        new("method", BlockFieldType.Text, Required: false),
        new("headers", BlockFieldType.Map, Required: false),
        new("body", BlockFieldType.Map, Required: false),
        new("expect", BlockFieldType.Text, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"url":"https://api.example.com/items","method":"POST","body":{"name":"{{triage.summary}}"},"expect":"200,201"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = Build(context.Config);
        var expect = ExpectedStatuses.Parse(ConfigValues.Text(context.Config["expect"]));
        if (request.IsError)
        {
            return request.Error;
        }

        if (expect.IsError)
        {
            return expect.Error;
        }

        var answer = await http.Send(request.Value, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        return expect.Value.Allows(answer.Value.Status)
            ? new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["status"] = answer.Value.Status, ["body"] = Json(answer.Value.Body), ["text"] = answer.Value.Body })
            : SindriErrors.GraphHttpFailed.With($"the answer was {answer.Value.Status.ToString(CultureInfo.InvariantCulture)}, and 'expect' allows {expect.Value.Described} (fix: change 'expect', or the request)");
    }

    private static Result<GraphHttpRequest> Build(JsonObject config)
    {
        var url = ConfigValues.Text(config["url"]) ?? string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return SindriErrors.GraphBlockFailed.With($"'url' must be a whole address such as https://api.example.com/items; '{url}' is not (fix: add the scheme and host)");
        }

        var method = (ConfigValues.Text(config["method"]) ?? "GET").Trim().ToUpperInvariant();
        if (!_methods.Contains(method, StringComparer.Ordinal))
        {
            return SindriErrors.GraphBlockFailed.With($"'method' must be one of {string.Join(", ", _methods)}; '{method}' is not");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (config["headers"] is JsonObject given)
        {
            foreach (var (name, value) in given)
            {
                headers[name] = ConfigValues.Text(value) ?? string.Empty;
            }
        }

        return new GraphHttpRequest(method, uri, headers, (config["body"] as JsonObject)?.ToJsonString());
    }

    // why: an answer that is not JSON (a page, plain words) is still the node's text; only a valid JSON document becomes 'body'
    private static JsonNode? Json(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (JsonException)   // boundary: the answer's text is not JSON
        {
            return null;
        }
    }
}
