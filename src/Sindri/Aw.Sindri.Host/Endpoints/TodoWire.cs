using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>What the todo routes read from the wire.</summary>
internal static class TodoWire
{
    /// <summary>Whether the request came with fence=1 (or true): the CLI had no terminal on stdout.</summary>
    /// <param name="fence">The query value.</param>
    /// <returns>True when fenced.</returns>
    internal static bool Piped(string? fence) => string.Equals(fence, "1", StringComparison.Ordinal) || string.Equals(fence, "true", StringComparison.Ordinal);

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
        text is null ? Kernel.Results.Success<TodoClientId?>(value: null)
        : TodoClientId.TryFrom(text, out var id) ? Kernel.Results.Success<TodoClientId?>(id)
        : SindriErrors.TodoText.With("A client id is 1 to 64 of A-Z, a-z, 0-9, '_' and '-'.");
}
