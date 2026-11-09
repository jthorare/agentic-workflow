using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.CheckTodoNote;
using Aw.Sindri.Features.MarkTodo;
using Aw.Sindri.Features.SetTodoNote;
using Aw.Sindri.Features.StopTodoNote;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The todo writes (todo spec D2, §4.4): add, change, and the four state verbs as variants of 'MarkTodo'. Request class, so the app, the terminal, MCP sessions and Sindri-launched sessions can all run them;
/// each waits for the views because it reads the todo's row. Remove is soft (Undo is restore), so none is destructive. The note's check is a command that writes nothing (self-audited, so no 'command.handled' row);
/// 'todo note set' and 'stop' are commands too (the Settings card's Start and Stop syncing); the CLI runs the same profile edit in process, with no control plane.
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
        new(ActionId.From("CheckTodoNote"), typeof(CheckTodoNoteRequest), Variant: null, ActionClass.Request, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: true, Destructive: false, ReadsViews: true, Shortcut: null,
            "POST", "/sindri/todos/note/check", ShellRows.Form("sindri todo note set <vault> <note> [--json]"), "ready"),
        new(ActionId.From("SetTodoNote"), typeof(SetTodoNoteRequest), Variant: null, ActionClass.Request, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/todos/note/set", ShellRows.Form("sindri todo note set <vault> <note> [--json]"), "ready"),
        new(ActionId.From("StopTodoNote"), typeof(StopTodoNoteRequest), Variant: null, ActionClass.Request, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/todos/note/stop", ShellRows.Form("sindri todo note stop [--json]"), "ready"),
    ];

    private static CommandRow Mark(string variant, string cli) =>
        Row("MarkTodo:" + variant, typeof(MarkTodoRequest), variant, "/sindri/todos/{todoId}/" + variant, cli);

    private static CommandRow Row(string action, Type request, string? variant, string route, string cli) =>
        new(ActionId.From(action), request, variant, ActionClass.Request, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: true, Shortcut: null,
            "POST", route, ShellRows.Form(cli), "ready");
}
