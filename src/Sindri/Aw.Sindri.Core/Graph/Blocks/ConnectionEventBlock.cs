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
    public string Title => "Connection event";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Triggers;

    /// <inheritdoc />
    public bool IsTrigger => true;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("connection", BlockFieldType.Text, Caption: "Connection"), new("event", BlockFieldType.Text, Caption: "Event"), new("filter", BlockFieldType.Text, Required: false, Caption: "Only when")]);

    /// <inheritdoc />
    public string Example => """{"connection":"github:work","event":"github.pr.merged","filter":"trigger.repo == \"acme/api\""}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Text(config, "event") is { } kind && EventKindSchemas.Find(kind) is { } schema ? new BlockOutputs(schema.Fields) : _outputs;
    }

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var problems = new List<(string Field, string Problem)>();
        var service = Text(config, "connection") is { } connection ? ServiceOf(connection, problems) : null;
        if (Text(config, "event") is { } kind && CheckEvent(kind, service) is { } problem)
        {
            problems.Add(("event", problem));
        }

        if (Text(config, "filter") is { } filter && !ConditionExpression.TryParse(filter, out _, out var error))
        {
            problems.Add(("filter", $"can't be read: {error}"));
        }

        return problems;
    }

    private static string? ServiceOf(string connection, List<(string Field, string Problem)> problems)
    {
        var name = ConnectionName.Parse(connection);
        if (name.IsSuccess)
        {
            return name.Value.Service;
        }

        problems.Add(("connection", "is not a connection name ('<service>:<label>', for example 'github:work')"));
        return null;
    }

    /// <summary>The poller of a service refuses the kinds of other services, so a kind of another service never fires; with no usable connection, only the catalog is checked.</summary>
    private static string? CheckEvent(string kind, string? service)
    {
        if (service is null)
        {
            return PollCatalog.Find(kind) is null ? $"'{kind}' is not a pollable event; the catalog lists them per connection (workflow_blocks)" : null;
        }

        var kinds = PollCatalog.For(service);
        if (kinds.Any(k => string.Equals(k.Key, kind, StringComparison.Ordinal)))
        {
            return null;
        }

        var listed = kinds.Count == 0 ? $"{service} connections have no pollable events" : $"{service} connections can poll: {string.Join(", ", kinds.Select(static k => k.Key))}";
        return $"'{kind}' is not an event of this connection's service; {listed}";
    }

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var name in OutputsFor(context.Config).Fields.Keys.Select(static k => k.TrimEnd('?')))
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
