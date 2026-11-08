using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.MarkTodo;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The todo writes (todo spec D2, §4.4): add, change, and the four state verbs as variants of 'MarkTodo'. Request class, so the app, the terminal, MCP sessions and Sindri-launched sessions can all run them;
/// each waits for the views because it reads the todo's row. Remove is soft (Undo is restore), so none is destructive.
/// </summary>
internal static class TodoRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Row("AddTodo", typeof(AddTodoRequest), variant: null, "/sindri/todos", "sindri todo add <text> [--due <day>] [--link <ref>]... [--client-id <id>]"),
        Row("ChangeTodo", typeof(ChangeTodoRequest), variant: null, "/sindri/todos/{todoId}/change", "sindri todo edit <id> [--text <text>] [--due <day> | --no-due] [--link <ref>]... [--no-links] [--version <n>]"),
        Mark("done", "sindri todo done <id> [--version <n>]"),
        Mark("reopen", "sindri todo reopen <id> [--version <n>]"),
        Mark("remove", "sindri todo remove <id> [--version <n>]"),
        Mark("restore", "sindri todo restore <id> [--version <n>]"),
    ];

    private static CommandRow Mark(string variant, string cli) =>
        Row("MarkTodo:" + variant, typeof(MarkTodoRequest), variant, "/sindri/todos/{todoId}/" + variant, cli);

    private static CommandRow Row(string action, Type request, string? variant, string route, string cli) =>
        new(ActionId.From(action), request, variant, ActionClass.Request, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: true, Shortcut: null,
            "POST", route, ShellRows.Form(cli), "ready");
}
