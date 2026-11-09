using System.Text.Json;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.MarkTodo;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Mcp;

/// <summary>Binds the arguments of sindri_todos and sindri_todo: the shape here (SND-CONTEXT-022), what a value means in the route's own rules (so the codes match HTTP), and the two rules a schema can't say (SND-TODO-001).</summary>
internal static class TodoBindings
{
    /// <summary>Every argument sindri_todo takes.</summary>
    internal static readonly string[] WriteNames = ["action", "id", "version", "text", "due", "links", "clientId", "side", "noteHash", "vault", "note", "expectOpen"];

    internal static Result<TodoListState> State(IDictionary<string, JsonElement>? arguments)
    {
        if (McpArguments.Only(SindriTools.Todos, arguments, "state") is { } refused)
        {
            return refused;
        }

        var text = McpArguments.OptionalString(SindriTools.Todos, arguments, "state");
        return text.Bind(t => t switch
        {
            null or "open" => Kernel.Results.Success(TodoListState.Open),
            "done" => TodoListState.Done,
            "all" => TodoListState.All,
            _ => ContextErrors.McpArgumentsInvalid.With($"sindri_todos 'state' is one of {string.Join(", ", SindriTools.TodoStates)} (fix: call it with {{\"state\": \"open\"}})"),
        });
    }

    internal static Result<(string Action, ICommand<TodoAccepted> Request)> Write(IDictionary<string, JsonElement>? arguments)
    {
        if (McpArguments.Only(SindriTools.Todo, arguments, WriteNames) is { } refused)
        {
            return refused;
        }

        var action = Action(arguments);
        if (action.IsError)
        {
            return action.Error;
        }

        if (Array.IndexOf(SindriTools.TodoWriteActions, action.Value) < 0)
        {
            return ContextErrors.McpArgumentsInvalid.With($"'{action.Value}' isn't a todo write (fix: see sindri_todo's input schema)");
        }

        var request = action.Value == "add" ? Add(arguments) : Existing(action.Value, arguments);
        return request.Map(r => (action.Value, r));
    }

    /// <summary>The pick or import action the call names, or null when it names a write or none (the binder then refuses a bad action).</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <returns>The action.</returns>
    internal static string? PickAction(IDictionary<string, JsonElement>? arguments) =>
        arguments is not null && arguments.TryGetValue("action", out var raw) && raw.ValueKind == JsonValueKind.String && raw.GetString() is { } name && Array.IndexOf(SindriTools.TodoPickActions, name) >= 0 ? name : null;

    private static Result<string> Action(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is not null && arguments.TryGetValue("action", out var raw) && raw.ValueKind == JsonValueKind.String && raw.GetString() is { } name
            && Array.IndexOf(SindriTools.TodoActions, name) >= 0)
        {
            return name;
        }

        return ContextErrors.McpArgumentsInvalid.With($"sindri_todo needs 'action', one of {string.Join(", ", SindriTools.TodoActions)} (fix: call it with {{\"action\": \"{SindriTools.TodoActions[0]}\"}})");
    }

    private static Result<ICommand<TodoAccepted>> Add(IDictionary<string, JsonElement>? arguments)
    {
        var text = McpArguments.RequiredString(SindriTools.Todo, arguments, "text", "call the dentist");
        var due = McpArguments.OptionalString(SindriTools.Todo, arguments, "due").Bind(TodoWire.Due);
        var links = Links(arguments);
        var client = McpArguments.OptionalString(SindriTools.Todo, arguments, "clientId").Bind(TodoWire.Client);
        return text.Bind(t => due.Bind(d => links.Bind(l => client.Map(c => (ICommand<TodoAccepted>)new AddTodoRequest(t, d.Date, l, c)))));
    }

    private static Result<ICommand<TodoAccepted>> Existing(string action, IDictionary<string, JsonElement>? arguments)
    {
        var id = McpArguments.RequiredString(SindriTools.Todo, arguments, "id", "td_...").Bind(TodoWire.Id);
        var version = McpArguments.OptionalLong(SindriTools.Todo, arguments, "version");
        if (action != "change")
        {
            var verb = action switch { "done" => TodoVerb.Done, "reopen" => TodoVerb.Reopen, "remove" => TodoVerb.Remove, _ => TodoVerb.Restore };
            return id.Bind(i => version.Map(v => (ICommand<TodoAccepted>)new MarkTodoRequest(i, verb, v)));
        }

        var text = McpArguments.OptionalString(SindriTools.Todo, arguments, "text");
        var due = McpArguments.OptionalString(SindriTools.Todo, arguments, "due").Bind(TodoWire.Due);
        var links = Links(arguments);
        return id.Bind(i => version.Bind(v => text.Bind(t => due.Bind(d => links.Bind(l => Change(i, v, t, d, l))))));
    }

    // why: the schema can't say "a version and one of three fields", so the rule is SND-TODO-001 naming it, the same text the route gives
    private static Result<ICommand<TodoAccepted>> Change(Core.Ids.TodoId id, long? version, string? text, (DateOnly? Date, bool Clear) due, IReadOnlyList<string>? links)
    {
        if (version is not { } seen)
        {
            return SindriErrors.TodoText.With("change needs a version and at least one of text, due, links.");
        }

        if (TodoDecisions.NoEdit(text, due.Date, due.Clear, links) is { } empty)
        {
            return empty;
        }

        return new ChangeTodoRequest(id, seen, text, due.Date, due.Clear, links);
    }

    private static Result<IReadOnlyList<string>?> Links(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || !arguments.TryGetValue("links", out var raw) || raw.ValueKind == JsonValueKind.Null)
        {
            return Kernel.Results.Success<IReadOnlyList<string>?>(value: null);
        }

        if (raw.ValueKind != JsonValueKind.Array || raw.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
        {
            return ContextErrors.McpArgumentsInvalid.With("'links' must be a list of strings (fix: see sindri_todo's input schema)");
        }

        return Kernel.Results.Success<IReadOnlyList<string>?>([.. raw.EnumerateArray().Select(e => e.GetString() ?? string.Empty)]);
    }
}
