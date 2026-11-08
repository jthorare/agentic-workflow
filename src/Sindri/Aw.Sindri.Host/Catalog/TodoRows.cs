using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.KeepTodo;
using Aw.Sindri.Features.KeepTodoUndo;
using Aw.Sindri.Features.MarkTodo;
using Aw.Sindri.Features.PickTodoHold;
using Aw.Sindri.Features.UndoTodoHold;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The todo writes (todo spec D2, §4.4): add, change, the four state verbs as variants of 'MarkTodo', the conflict pick and its Undo, and the hold picks (apply and restore are variants of 'PickTodoHold') and their Undo. Request class, so the app, the terminal, MCP sessions and Sindri-launched sessions can all run them;
/// each waits for the views because it reads the todo's row. Remove is soft (Undo is restore), so none is destructive.
/// </summary>
internal static class TodoRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        Row("AddTodo", typeof(AddTodoRequest), variant: null, "/sindri/todos", "sindri todo add <text> [--due <day>] [--link <ref>] [--client-id <id>]"),
        Row("ChangeTodo", typeof(ChangeTodoRequest), variant: null, "/sindri/todos/{todoId}/change", "sindri todo edit <id> [--text <text>] [--due <day>] [--no-due] [--link <ref>] [--no-links] [--version <n>]"),
        Mark("done", "sindri todo done <id> [--version <n>]"),
        Mark("reopen", "sindri todo reopen <id> [--version <n>]"),
        Mark("remove", "sindri todo remove <id> [--version <n>]"),
        Mark("restore", "sindri todo restore <id> [--version <n>]"),
        Row("KeepTodo", typeof(KeepTodoRequest), variant: null, "/sindri/todos/{todoId}/keep", "sindri todo keep <id> sindri|obsidian"),
        Row("KeepTodoUndo", typeof(KeepTodoUndoRequest), variant: null, "/sindri/todos/{todoId}/keep/undo", "sindri todo keep <id> --undo"),
        Hold("apply"),
        Hold("restore"),
        Row("UndoTodoHold", typeof(UndoTodoHoldRequest), variant: null, "/sindri/todos/note/hold/undo", "sindri todo note hold undo"),
    ];

    // why: a pick's Undo and the hold picks have no CLI verb yet (todo spec §4.4: an agent uses sindri_todo's actions); their forms only name the action so the catalog's CLI column stays a valid form
    private static CommandRow Hold(string variant) =>
        Row("PickTodoHold:" + variant, typeof(PickTodoHoldRequest), variant, "/sindri/todos/note/hold/" + variant, "sindri todo note hold " + variant);

    private static CommandRow Mark(string variant, string cli) =>
        Row("MarkTodo:" + variant, typeof(MarkTodoRequest), variant, "/sindri/todos/{todoId}/" + variant, cli);

    private static CommandRow Row(string action, Type request, string? variant, string route, string cli) =>
        new(ActionId.From(action), request, variant, ActionClass.Request, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: true, Shortcut: null,
            "POST", route, ShellRows.Form(cli), "ready");
}
