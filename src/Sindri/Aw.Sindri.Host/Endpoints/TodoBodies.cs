using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.MarkTodo;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/todos (todo spec §4.4): <see cref="AddTodoRequest"/>'s fields with the due date as a string ('yyyy-MM-dd', or absent) and the client id as text, so a bad one is a coded refusal.</summary>
/// <param name="Text">The text.</param>
/// <param name="Due">The due date as 'yyyy-MM-dd', or null; "" means none.</param>
/// <param name="Links">The typed links, or null.</param>
/// <param name="ClientId">The caller's retry id, or null.</param>
internal sealed record AddTodoBody(string? Text = null, string? Due = null, IReadOnlyList<string>? Links = null, string? ClientId = null)
{
    /// <summary>The request.</summary>
    /// <param name="piped">Whether the request came with fence=1.</param>
    /// <returns>The request, SND-TODO-001 for no text or a bad client id, or SND-TODO-003 for a bad date.</returns>
    internal Result<AddTodoRequest> ToRequest(bool piped)
    {
        if (Text is null)
        {
            return SindriErrors.TodoText.With("Add the todo's text.");
        }

        var due = TodoWire.Due(Due);
        if (due.IsError)
        {
            return due.Error;
        }

        var client = TodoWire.Client(ClientId);
        return client.IsError ? client.Error : new AddTodoRequest(Text, due.Value.Date, Links, client.Value, piped);
    }
}

/// <summary>The body of POST /sindri/todos/{id}/change: the version the caller saw and what to change. A field left out (or null) stays as it was; "" in <c>due</c> drops the date and [] in <c>links</c> drops the links.</summary>
/// <param name="Version">The version the caller saw (required).</param>
/// <param name="Text">New text, or null.</param>
/// <param name="Due">A new due date as 'yyyy-MM-dd', "" to clear it, or null to leave it.</param>
/// <param name="Links">The new whole list of links, or null.</param>
internal sealed record ChangeTodoBody(long? Version = null, string? Text = null, string? Due = null, IReadOnlyList<string>? Links = null)
{
    /// <summary>The request for <paramref name="id"/>.</summary>
    /// <param name="id">The todo from the route.</param>
    /// <param name="piped">Whether the request came with fence=1.</param>
    /// <returns>The request, SND-TODO-001 for no version, or SND-TODO-003 for a bad date.</returns>
    internal Result<ChangeTodoRequest> ToRequest(TodoId id, bool piped)
    {
        if (Version is not { } version)
        {
            return SindriErrors.TodoText.With("change needs a version and at least one of text, due, links.");
        }

        var due = TodoWire.Due(Due);
        return due.IsError ? due.Error : new ChangeTodoRequest(id, version, Text, due.Value.Date, due.Value.Clear, Links, piped);
    }
}

/// <summary>The body of POST /sindri/todos/{id}/done, /reopen, /remove and /restore: the version the caller saw, or none for the current one.</summary>
/// <param name="Version">The version the caller saw, or null.</param>
internal sealed record MarkTodoBody(long? Version = null)
{
    /// <summary>The request.</summary>
    /// <param name="id">The todo from the route.</param>
    /// <param name="verb">What to do.</param>
    /// <param name="piped">Whether the request came with fence=1.</param>
    /// <returns>The request.</returns>
    internal MarkTodoRequest ToRequest(TodoId id, TodoVerb verb, bool piped) => new(id, verb, Version, piped);
}

/// <summary>What the todo routes read from the wire.</summary>
internal static class TodoWire
{
    /// <summary>Whether the request came with fence=1 (or true): the CLI had no terminal on stdout.</summary>
    /// <param name="fence">The query value.</param>
    /// <returns>True when fenced.</returns>
    internal static bool Piped(string? fence) => fence is "1" or "true";

    /// <summary>The todo in a '{todoId}' segment.</summary>
    /// <param name="todoId">The segment.</param>
    /// <returns>The id, or SND-CLI-003.</returns>
    internal static Result<TodoId> Id(string todoId) =>
        TodoId.TryFrom(todoId, out var id) ? id : SindriErrors.InvalidInput.With($"'{Aw.Sindri.Core.Text.SafeText.Line(Aw.Sindri.Core.Text.SafeText.Escape(todoId), 80)}' isn't a todo id (td_…)");

    /// <summary>A due date as the routes take it: absent or null leaves it, "" clears it, 'yyyy-MM-dd' sets it.</summary>
    /// <param name="text">The JSON string.</param>
    /// <returns>The date and whether to clear it, or SND-TODO-003.</returns>
    internal static Result<(DateOnly? Date, bool Clear)> Due(string? text) =>
        text is null ? (null, false)
        : text.Length == 0 ? (null, true)
        : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? (date, false)
        : SindriErrors.TodoDate.With("Dates are YYYY-MM-DD, or \"\" to clear.");

    /// <summary>A client id as text.</summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The id (null for none), or SND-TODO-001.</returns>
    internal static Result<TodoClientId?> Client(string? text) =>
        text is null ? Results.Success<TodoClientId?>(null)
        : TodoClientId.TryFrom(text, out var id) ? Results.Success<TodoClientId?>(id)
        : SindriErrors.TodoText.With("A client id is 1 to 64 of A-Z, a-z, 0-9, '_' and '-'.");
}
