using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetTodo;
using Aw.Sindri.Features.GetTodoNote;
using Aw.Sindri.Features.ListTodos;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri todos' and 'sindri todo show|add|edit|done|reopen|remove|restore|note' (todo spec §4.4): clients of the todo routes. Every verb starts the control plane unless --no-start; inside an agent session it never does
/// (API spec §7). A verb that takes an id resolves a unique prefix among the todos it acts on (<see cref="TodoPrefix"/>); edit and the state verbs read the todo's version first when --version is left out, as 'item close' does.
/// Text is fenced (fence=1) whenever stdout isn't a terminal, unless --no-fence says the pipe is the owner's own.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
/// <param name="stdoutIsTerminal">Whether stdout is a terminal; the default asks the console.</param>
/// <param name="today">The local date, for due words; the default is this machine's.</param>
internal sealed partial class TodoVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null, Func<bool>? stdoutIsTerminal = null, Func<DateOnly>? today = null)
{
    private const string Root = "/sindri/todos";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = TodoVerbArguments.Parse(args, (today ?? Today)());
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var command = parsed.Value;
        var down = ensureRunning is null ? null : await ensureRunning(!command.NoStart, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        return command.Verb switch
        {
            TodoVerbArguments.Verb.List => await List(command, ct).ConfigureAwait(false),
            TodoVerbArguments.Verb.Show => await Show(command, ct).ConfigureAwait(false),
            TodoVerbArguments.Verb.Note => await Note(command, ct).ConfigureAwait(false),
            TodoVerbArguments.Verb.Keep => await Keep(command, ct).ConfigureAwait(false),
            TodoVerbArguments.Verb.None or TodoVerbArguments.Verb.Add or TodoVerbArguments.Verb.Edit or TodoVerbArguments.Verb.Done or TodoVerbArguments.Verb.Reopen or TodoVerbArguments.Verb.Remove or TodoVerbArguments.Verb.Restore
                => await Write(command, ct).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(args), command.Verb, "not a todo verb"),
        };
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    private bool Fenced(TodoVerbArguments.Parsed command) => command.Fence ?? !(stdoutIsTerminal?.Invoke() ?? !Console.IsOutputRedirected);

    private string Query(TodoVerbArguments.Parsed command, string? state = null) =>
        UrlQuery.Of([("state", state), ("fence", Fenced(command) ? "1" : null)]);

    private async Task<int> List(TodoVerbArguments.Parsed command, CancellationToken ct)
    {
        var answer = await client.GetView<ListTodosResponse>(Root + Query(command, command.State), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var list = answer.Value.Value;
        var code = Stale(list.AsOf) ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(RenderList(list), code).ConfigureAwait(false);
    }

    private async Task<int> Show(TodoVerbArguments.Parsed command, CancellationToken ct)
    {
        var id = await Resolve(command.Id, TodoCliVerb.Show, ct).ConfigureAwait(false);
        if (id.IsError)
        {
            return await output.Error(id.Error).ConfigureAwait(false);
        }

        var answer = await client.GetView<GetTodoResponse>(Root + "/" + id.Value.Value + Query(command), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = Stale(answer.Value.Value.AsOf) ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(RenderOne(answer.Value.Value.Todo), code).ConfigureAwait(false);
    }

    private async Task<int> Note(TodoVerbArguments.Parsed command, CancellationToken ct)
    {
        var answer = await client.GetView<GetTodoNoteResponse>(Root + "/note", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = Stale(answer.Value.Value.AsOf) ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(answer.Value.Value.State + "\n", code).ConfigureAwait(false);
    }

    private async Task<int> Write(TodoVerbArguments.Parsed command, CancellationToken ct)
    {
        var sent = await Send(command, ct).ConfigureAwait(false);
        if (sent.IsError)
        {
            return await output.Error(sent.Error).ConfigureAwait(false);
        }

        return command.Json
            ? await output.Raw(sent.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(Line(command, sent.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<Result<ApiAnswer<TodoAccepted>>> Send(TodoVerbArguments.Parsed command, CancellationToken ct)
    {
        var fence = Fenced(command) ? "?fence=1" : string.Empty;
        if (command.Verb == TodoVerbArguments.Verb.Add)
        {
            var body = new AddTodoBody(command.Text, Iso(command.Due), command.Links.Count == 0 ? null : command.Links, command.ClientId ?? Guid.NewGuid().ToString("N"));
            return await client.Post<AddTodoBody, TodoAccepted>(Root + fence, body, ct).ConfigureAwait(false);
        }

        var id = await Resolve(command.Id, CliVerb(command.Verb), ct).ConfigureAwait(false);
        if (id.IsError)
        {
            return id.Error;
        }

        var version = await Version(command, id.Value, ct).ConfigureAwait(false);
        if (version.IsError)
        {
            return version.Error;
        }

        var route = Root + "/" + id.Value.Value;
        return command.Verb == TodoVerbArguments.Verb.Edit
            ? await client.Post<ChangeTodoBody, TodoAccepted>(route + "/change" + fence, EditBody(command, version.Value), ct).ConfigureAwait(false)
            : await client.Post<MarkTodoBody, TodoAccepted>(route + "/" + command.Verb.ToString().ToLowerInvariant() + fence, new MarkTodoBody(version.Value), ct).ConfigureAwait(false);
    }

    private static ChangeTodoBody EditBody(TodoVerbArguments.Parsed command, long version) =>
        new(version, command.Text, command.ClearDue ? string.Empty : Iso(command.Due), command.NoLinks ? [] : command.Links.Count == 0 ? null : command.Links);

    private static string? Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static TodoCliVerb CliVerb(TodoVerbArguments.Verb verb) => verb switch
    {
        TodoVerbArguments.Verb.Edit => TodoCliVerb.Edit,
        TodoVerbArguments.Verb.Keep => TodoCliVerb.Keep,
        TodoVerbArguments.Verb.Done => TodoCliVerb.Done,
        TodoVerbArguments.Verb.Reopen => TodoCliVerb.Reopen,
        TodoVerbArguments.Verb.Remove => TodoCliVerb.Remove,
        TodoVerbArguments.Verb.Restore => TodoCliVerb.Restore,
        TodoVerbArguments.Verb.None or TodoVerbArguments.Verb.List or TodoVerbArguments.Verb.Show or TodoVerbArguments.Verb.Add or TodoVerbArguments.Verb.Note => throw new ArgumentOutOfRangeException(nameof(verb), verb, "not a verb that takes an id"),
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "not a todo verb"),
    };

    /// <summary>The id the user typed: a full id, a unique prefix among the todos the verb acts on, or the note's '^td-…' form.</summary>
    private async Task<Result<TodoId>> Resolve(string typed, TodoCliVerb verb, CancellationToken ct)
    {
        var all = await client.GetView<ListTodosResponse>(Root + "?state=all", ct).ConfigureAwait(false);
        return all.IsError ? all.Error : TodoPrefix.Resolve(typed, verb, [.. all.Value.Value.Todos.Select(static t => new TodoCandidate(t.Id, t.DoneAt is not null, t.Removed))]);
    }

    /// <summary>The version a write sends: the one typed, or the todo's own, read now (as 'item close' does).</summary>
    private async Task<Result<long>> Version(TodoVerbArguments.Parsed command, TodoId id, CancellationToken ct)
    {
        if (command.Version is { } typed)
        {
            return typed;
        }

        var read = await client.GetView<GetTodoResponse>(Root + "/" + id.Value, ct).ConfigureAwait(false);
        return read.IsError ? read.Error : read.Value.Value.Todo.Version;
    }

    private static bool Stale(AsOf asOf) => asOf.Rebuilding || asOf.Stalled;

    private static string Line(TodoVerbArguments.Parsed command, TodoAccepted accepted)
    {
        var id = accepted.Id.Value;
        return command.Verb switch
        {
            TodoVerbArguments.Verb.Add => accepted.Due is { } due ? $"{id} added (due {Day(due)})\n" : $"{id} added\n",
            TodoVerbArguments.Verb.Edit => $"{id} changed\n",
            TodoVerbArguments.Verb.Done => $"{id} done\n",
            TodoVerbArguments.Verb.Reopen => $"{id} reopened\n",
            TodoVerbArguments.Verb.Remove => $"{id} removed (sindri todo restore {id})\n",
            TodoVerbArguments.Verb.Restore => $"{id} restored\n",
            TodoVerbArguments.Verb.None or TodoVerbArguments.Verb.List or TodoVerbArguments.Verb.Show or TodoVerbArguments.Verb.Note or TodoVerbArguments.Verb.Keep => throw new ArgumentOutOfRangeException(nameof(command), command.Verb, "not a plain write"),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.Verb, "not a todo verb"),
        };
    }

    private static string Day(DateOnly date) => date.ToString("ddd yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string RenderList(ListTodosResponse list)
    {
        var text = new StringBuilder();
        var all = list.State == TodoListState.All;
        foreach (var row in list.Todos)
        {
            _ = text.AppendLine(RowLine(row, all));
        }

        return text.ToString();
    }

    private static string RowLine(TodoRow row, bool withState) =>
        RowLine(new RowFields(row.Id, StateOf(row), row.Due, row.Text, row.Links, row.Conflict is not null), withState);

    private static string RowLine(TodoItem row, bool withState) =>
        RowLine(new RowFields(row.Id, row.Removed ? "removed" : row.DoneAt is not null ? "done" : "open", row.Due, row.Text, [.. row.Links.Select(static l => l.Href)], row.Conflict is not null), withState);

    private sealed record RowFields(TodoId Id, string State, DateOnly? Due, string Text, IReadOnlyList<string> Links, bool Conflict);

    private static string RowLine(RowFields row, bool withState)
    {
        var (id, state, due, text, links, conflict) = row;
        var line = new StringBuilder(id.Value).Append("  ");
        if (withState)
        {
            _ = line.Append(state.PadRight(7)).Append("  ");
        }

        _ = line.Append(due is { } day ? day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "-").Append("  ").Append(text);
        if (links.Count > 0)
        {
            _ = line.Append("  [").AppendJoin(' ', links).Append(']');
        }

        if (conflict)
        {
            _ = line.Append("  (changed in both places: sindri todo show ").Append(id.Value).Append(')');
        }

        return line.ToString();
    }

    private static string StateOf(TodoRow row) => row.Removed ? "removed" : row.DoneAt is not null ? "done" : "open";

    private static string RenderOne(TodoRow row)
    {
        var text = new StringBuilder();
        _ = text.AppendLine(RowLine(row, withState: true));
        Field(text, "added", $"{Instant(row.AddedAt)} by {row.AddedBy}{(row.AddedVia is { } via ? $" ({via})" : string.Empty)}");
        Field(text, "last changed by", $"{row.LastBy}{(row.LastVia is { } last ? $" ({last})" : string.Empty)}");
        Field(text, "version", row.Version.ToString(CultureInfo.InvariantCulture));
        if (row.DoneAt is { } done)
        {
            Field(text, "done", Instant(done));
        }

        if (row.Removed)
        {
            Field(text, "removed", $"{(row.RemovedAt is { } at ? Instant(at) : "-")} by {row.RemovedBy ?? "-"}");
        }

        if (row.Conflict is { } conflict)
        {
            Field(text, "in Sindri", Side(conflict.Sindri));
            Field(text, "in Obsidian", Side(conflict.Obsidian));
        }

        return text.ToString();
    }

    private static string Side(Core.Events.TodoSide side) =>
        side.Removed ? "removed" : $"{(side.Done ? "[done] " : string.Empty)}{side.Text}{(side.Due is { } due ? " due " + due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty)}";

    private static string Instant(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static void Field(StringBuilder text, string name, string value) => text.Append("  ").Append(name).Append(": ").AppendLine(value);
}
