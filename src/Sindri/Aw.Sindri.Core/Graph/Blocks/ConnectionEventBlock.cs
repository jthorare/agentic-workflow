using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph.Expressions;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'connection_event' trigger (#2258, PR 11): starts a run when a connection's provider reports an event of one catalog kind ('github.pr.merged'). The poll service makes one provider
/// call per (connection, event kind) however many triggers watch it, then keeps the events whose fields satisfy this trigger's 'filter' (the condition language, fields read as 'trigger.title').
/// The run's occurrence is the event's delivery id, so one provider event starts one run. The values are provider text: untrusted, fenced wherever they reach a prompt.
/// </summary>
public sealed class ConnectionEventBlock : IGraphBlock
{
    /// <summary>The names the trigger hands on when the event has them; the poll service puts each one it has under 'trigger'.</summary>
    private static readonly string[] _optionalFields = ["repo", "number", "title", "url", "author", "ref", "identifier", "label", "to", "channel", "ts", "user", "text", "start"];

    private static readonly BlockOutputs _outputs = new(Fields());

    /// <inheritdoc />
    public string Type => "connection_event";

    /// <inheritdoc />
    public bool IsTrigger => true;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("connection", BlockFieldType.Text), new("event", BlockFieldType.Text), new("filter", BlockFieldType.Text, Required: false)]);

    /// <inheritdoc />
    public string Example => """{"connection":"github:work","event":"github.pr.merged","filter":"trigger.repo == \"acme/api\""}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var problems = new List<(string Field, string Problem)>();
        if (Text(config, "connection") is { } connection && !ConnectionName.Parse(connection).IsSuccess)
        {
            problems.Add(("connection", "is not a connection name ('<service>:<label>', for example 'github:work')"));
        }

        if (Text(config, "event") is { } kind && PollCatalog.Find(kind) is null)
        {
            problems.Add(("event", $"'{kind}' is not a pollable event; the catalog lists them per connection (workflow_blocks)"));
        }

        if (Text(config, "filter") is { } filter && !ConditionExpression.TryParse(filter, out _, out var error))
        {
            problems.Add(("filter", $"can't be read: {error}"));
        }

        return problems;
    }

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var name in Fields().Keys.Select(static k => k.TrimEnd('?')))
        {
            values[name] = context.Resolve("trigger." + name);
        }

        return ValueTask.FromResult<Result<BlockResult>>(new BlockResult(values));
    }

    private static Dictionary<string, string> Fields()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["deliveryId"] = "string", ["occurredAt"] = "string", ["connection"] = "string", ["event"] = "string" };
        foreach (var name in _optionalFields)
        {
            fields[name + "?"] = "string";
        }

        return fields;
    }

    private static string? Text(JsonObject config, string name) => config[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;
}
