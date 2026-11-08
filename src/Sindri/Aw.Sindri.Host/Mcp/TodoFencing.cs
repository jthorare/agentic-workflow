using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.Trust;
using Aw.Kernel.Json;
using Aw.Sindri.Features.GetTodo;
using Aw.Sindri.Features.ListTodos;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The todo routes' fencing (todo spec §4.4): the list and one todo through <see cref="McpFencing"/> with <see cref="FencePlan.Todos"/> and <see cref="FencePlan.OneTodo"/>, and the ledger's 'todo.*' payloads by shape:
/// every string but the ids, hashes, enum values, dates, 'by' and 'via'.
/// </summary>
/// <param name="json">The host's JSON options.</param>
internal sealed class TodoFencing(JsonSerializerOptions json) : ITodoFencing
{
    private const string Source = "sindri:todo";

    // the payload members that are never free text
    private static readonly HashSet<string> _plain = new(StringComparer.Ordinal) { "id", "ids", "noteHash", "baseHash", "hash", "kept", "by", "via", "due", "clientId", "vault", "note" };

    public IResult List(ListTodosResponse list) => Fenced(AwJson.Serialize(list, json), FencePlan.Todos);

    public IResult One(GetTodoResponse todo) => Fenced(AwJson.Serialize(todo, json), FencePlan.OneTodo);

    public IResult Ledger(LedgerPage page)
    {
        var node = JsonNode.Parse(AwJson.Serialize(page, json));
        if (node?["events"] is JsonArray events)
        {
            foreach (var @event in events.OfType<JsonObject>())
            {
                FenceEvent(@event);
            }
        }

        return TypedResults.Text(node?.ToJsonString(_write) ?? "null", "application/json", System.Text.Encoding.UTF8);
    }

    private static void FenceEvent(JsonObject @event)
    {
        if (@event["type"]?.GetValue<string>() is { } type && type.StartsWith("todo.", StringComparison.Ordinal) && @event["payload"] is JsonObject payload
            && FenceStrings(payload, payload["by"]?.GetValue<string>()))
        {
            payload[McpFencing.TrustMember] = McpFencing.Untrusted;
        }
    }

    private static readonly JsonSerializerOptions _write = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // fences every string under the object but the plain members; returns whether any was
    private static bool FenceStrings(JsonObject owner, string? author)
    {
        var any = false;
        foreach (var (name, child) in owner.ToList())
        {
            if (_plain.Contains(name) || McpFencing.TrustMember == name)
            {
                continue;
            }

            switch (child)
            {
                case JsonValue value when value.TryGetValue<string>(out var text):
                    owner[name] = UntrustedFence.Wrap(Source, author, text);
                    any = true;
                    break;
                case JsonObject nested:
                    any |= FenceStrings(nested, author);
                    break;
                case JsonArray array:
                    any |= FenceArray(array, author);
                    break;
                default:
                    break;
            }
        }

        return any;
    }

    private static bool FenceArray(JsonArray array, string? author)
    {
        var any = false;
        for (var i = 0; i < array.Count; i++)
        {
            switch (array[i])
            {
                case JsonValue value when value.TryGetValue<string>(out var text):
                    array[i] = UntrustedFence.Wrap(Source, author, text);
                    any = true;
                    break;
                case JsonObject nested:
                    any |= FenceStrings(nested, author);
                    break;
                case JsonArray inner:
                    any |= FenceArray(inner, author);
                    break;
                default:
                    break;
            }
        }

        return any;
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult Fenced(byte[] body, FencePlan plan)
    {
        using var document = JsonDocument.Parse(body);
        return TypedResults.Text(McpFencing.Apply(document.RootElement, plan).GetRawText(), "application/json", System.Text.Encoding.UTF8);
    }
}
